using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
	public abstract class DebugProtocol
	{
		private const int BufferSize = 4096;

		private static readonly Regex ContentLengthMatcher = new Regex("^(?<ignored>.*?)Content-Length: (?<length>\\d+)\r\n\r\n", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

		private const string ContentLengthFormat = "Content-Length: {0}\r\n\r\n";

		private static readonly Encoding ProtocolEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

		private const string WildcardId = "*";

		private int sequenceNumber;

		private Stream readStream;

		private Stream writeStream;

		private DebugProtocolOptions options;

		private ByteBuffer rawData;

		private int nextMessageBodyLength;

		private System.Threading.Thread inputReaderThread;

		private int dispatcherThreadId;

		private CancellationTokenSource tokenSource;

		private object dispatcherSyncObject = new object();

		private bool queueEvents;

		private bool isHandlingError;

		private List<IPendingRequest> pendingRequests;

		private ConcurrentQueue<ProtocolEvent> queuedEvents;

		private JsonSerializerSettings jsonSettings;

		private JsonSerializer jsonSerializer;

		private JsonSerializerSettings traceSettings;

		private Dictionary<string, IEventInfo> eventRegistrations;

		private Dictionary<string, IRequestInfo> requestRegistrations;

		private readonly StringComparison responseCommandStringComparison;

		private ManualResetEvent messagesPendingEvent;

		private bool isLogging;

		private object logSyncObj = new object();

		private Queue<byte[]> outgoingQueue = new Queue<byte[]>();

		private object outgoingSyncObj = new object();

		private bool isSendingMessages;

		public bool IsRunning
		{
			get;
			private set;
		}

		private CancellationToken CancellationToken => tokenSource.Token;

		private bool QueueEvents
		{
			get
			{
				lock (dispatcherSyncObject)
				{
					return queueEvents;
				}
			}
		}

		public event EventHandler<DispatcherErrorEventArgs> DispatcherError;

		public event EventHandler<RequestReceivedEventArgs> RequestReceived;

		public event EventHandler SynchronousOperationBegin;

		public event EventHandler SynchronousOperationEnd;

		public event EventHandler SynchronousOperationUpdate;

		public event EventHandler<LogEventArgs> LogMessage;

		public event EventHandler<RequestCompletedEventArgs> RequestCompleted;

		public event EventHandler<ResponseTimeThresholdExceededEventArgs> ResponseTimeThresholdExceeded;

		protected DebugProtocol(Stream readStream, Stream writeStream)
			: this(readStream, writeStream, DebugProtocolOptions.None)
		{
		}

		protected DebugProtocol(Stream readStream, Stream writeStream, DebugProtocolOptions options)
		{
			this.readStream = readStream;
			this.writeStream = Stream.Synchronized(writeStream);
			this.options = options;
			nextMessageBodyLength = -1;
			sequenceNumber = 0;
			rawData = new ByteBuffer(ProtocolEncoding);
			tokenSource = new CancellationTokenSource();
			pendingRequests = new List<IPendingRequest>();
			queuedEvents = new ConcurrentQueue<ProtocolEvent>();
			eventRegistrations = new Dictionary<string, IEventInfo>();
			requestRegistrations = new Dictionary<string, IRequestInfo>();
			messagesPendingEvent = new ManualResetEvent(initialState: true);
			jsonSettings = new JsonSerializerSettings
			{
				Converters = 
				{
					(JsonConverter)new FlagsEnumConverter(this),
					(JsonConverter)new StringEnumConverterWithDefault(this),
					(JsonConverter)new CustomSerializationConverter(this)
				},
				ContractResolver = CustomSerializationContractResolver.Instance
			};
			jsonSerializer = JsonSerializer.Create(jsonSettings);
			traceSettings = new JsonSerializerSettings
			{
				Converters = 
				{
					(JsonConverter)new FlagsEnumConverter(this),
					(JsonConverter)new StringEnumConverter
					{
						AllowIntegerValues = false
					},
					(JsonConverter)new CustomSerializationConverter(this)
				},
				ContractResolver = CustomSerializationContractResolver.Instance
			};
			responseCommandStringComparison = (options.HasFlag(DebugProtocolOptions.ResponseCommandCaseInsensitive) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		}

		public void Run()
		{
			if (inputReaderThread == null)
			{
				inputReaderThread = new System.Threading.Thread(ReaderThreadProc)
				{
					Name = "DebugAdapter Reader Thread"
				};
			}
			IsRunning = true;
			inputReaderThread.Start();
		}

		public void Stop()
		{
			Stop(2000);
		}

		public void Stop(int millisecondTimeout)
		{
			if (!messagesPendingEvent.WaitOne(millisecondTimeout))
			{
				this.LogMessage?.Invoke(this, new LogEventArgs(LogCategory.Warning, "Timeout hit in Stop()."));
			}
			lock (tokenSource)
			{
				tokenSource.Cancel();
				try
				{
					CancelAllPendingRequests();
				}
				catch (ProtocolException)
				{
				}
			}
		}

		public void WaitForReader()
		{
			WaitForReader(-1);
		}

		public bool WaitForReader(int millisecondsTimeout)
		{
			VerifySynchronousOperationAllowed();
			if (millisecondsTimeout == -1)
			{
				inputReaderThread.Join();
				return true;
			}
			return inputReaderThread.Join(millisecondsTimeout);
		}

		public void VerifySynchronousOperationAllowed()
		{
			if (System.Threading.Thread.CurrentThread.ManagedThreadId == dispatcherThreadId)
			{
				throw new InvalidOperationException("Operation not allowed on dispatcher thread!");
			}
		}

		public void CancelAllPendingRequests()
		{
			lock (pendingRequests)
			{
				if (pendingRequests.Any((IPendingRequest r) => !r.Cancelled))
				{
					Log(LogCategory.Debug, "Cancelling outstanding request(s) with ID(s): {0}".FormatInvariantWithArgs(string.Join(", ", from r in pendingRequests
						where !r.Cancelled
						select r.Request.Seq.ToString(CultureInfo.InvariantCulture))));
				}
				List<Exception> list = null;
				foreach (IPendingRequest pendingRequest in pendingRequests)
				{
					try
					{
						pendingRequest.Cancel();
					}
					catch (Exception item)
					{
						if (list == null)
						{
							list = new List<Exception>();
						}
						list.Add(item);
					}
				}
				if (list != null)
				{
					throw new ProtocolException("Exceptions thrown while cancelling pending requests", new AggregateException(list));
				}
			}
		}

		internal void RegisterEventTypeCore(string eventId, IEventInfo eventInfo)
		{
			if (string.Equals(eventId, "*", StringComparison.Ordinal) && !options.HasFlag(DebugProtocolOptions.AllowWildcardRegistrations))
			{
				throw new InvalidOperationException("Wildcard registrations are not allowed without the DebugProtcolOptions.AllowWildcardRegistrations flag set.");
			}
			eventRegistrations.Add(eventId, eventInfo);
		}

		internal void RegisterRequestTypeCore(string requestId, IRequestInfo requestInfo)
		{
			if (string.Equals(requestId, "*", StringComparison.Ordinal) && !options.HasFlag(DebugProtocolOptions.AllowWildcardRegistrations))
			{
				throw new InvalidOperationException("Wildcard registrations are not allowed without the DebugProtcolOptions.AllowWildcardRegistrations flag set.");
			}
			requestRegistrations.Add(requestId, requestInfo);
		}

		internal void NotifyRequestCompletion(RequestCompletedEventArgs args)
		{
			this.RequestCompleted?.Invoke(this, args);
		}

		internal void NotifyResponseTimeThresholdExceeded(ResponseTimeThresholdExceededEventArgs args)
		{
			this.ResponseTimeThresholdExceeded?.Invoke(this, args);
		}

		protected internal virtual void OnEventReceived(string eventType, DebugEvent body)
		{
			throw new InvalidOperationException("DebugProtocol-derived class does not handle messages of type ProtocolEvent!");
		}

		internal void OnRequestReceived(string command, IRequestResponder responder)
		{
			try
			{
				bool flag = string.Equals(command, "disconnect", StringComparison.Ordinal);
				if (flag)
				{
					lock (dispatcherSyncObject)
					{
						queueEvents = false;
					}
				}
				RequestReceivedEventArgs requestReceivedEventArgs = new RequestReceivedEventArgs(command, responder.Arguments);
				this.RequestReceived?.Invoke(this, requestReceivedEventArgs);
				if (requestReceivedEventArgs.Response == null)
				{
					throw new InvalidOperationException("RequestReceived event handler returned without setting a response!");
				}
				responder.SetResponse(requestReceivedEventArgs.Response);
				if (flag)
				{
					Stop();
				}
			}
			catch (ProtocolException error)
			{
				responder.SetError(error);
			}
			finally
			{
				ProcessQueuedEvents();
			}
		}

		internal void ProcessQueuedEvents()
		{
			ProtocolEvent result;
			while (queuedEvents.TryDequeue(out result))
			{
				SendMessageCore(result);
			}
		}

		private int GetNextSequenceNumber()
		{
			return Interlocked.Increment(ref sequenceNumber);
		}

		private void TraceMessage(ProtocolMessage message, string rawText, bool isSend, bool isCancelled = false, int? duration = null)
		{
			if (this.LogMessage != null)
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(isSend ? "--> " : "<--   ");
				stringBuilder.Append(message);
				if (isCancelled)
				{
					stringBuilder.Append(" [Cancelled]");
				}
				if (duration.HasValue)
				{
					stringBuilder.Append(" [{0} ms]".FormatInvariantWithArgs(duration.Value));
				}
				stringBuilder.Append(": ");
				if (!string.IsNullOrEmpty(rawText))
				{
					stringBuilder.Append(rawText);
				}
				else
				{
					stringBuilder.Append(JsonConvert.SerializeObject(message, traceSettings));
				}
				Log(LogCategory.Trace, stringBuilder.ToString());
			}
		}

		internal void Log(LogCategory category, string message)
		{
			if (!string.IsNullOrWhiteSpace(message))
			{
				lock (logSyncObj)
				{
					if (this.LogMessage != null && !isLogging)
					{
						try
						{
							isLogging = true;
							this.LogMessage(this, new LogEventArgs(category, message));
						}
						finally
						{
							isLogging = false;
						}
					}
				}
			}
		}

		private void ReaderThreadProc()
		{
			dispatcherThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
			byte[] array = new byte[4096];
			try
			{
				lock (dispatcherSyncObject)
				{
					isHandlingError = false;
				}
				while (!CancellationToken.IsCancellationRequested)
				{
					int result = readStream.ReadAsync(array, 0, array.Length, CancellationToken).Result;
					if (result == 0)
					{
						Stop();
						return;
					}
					rawData.AppendData(array, result);
					while ((nextMessageBodyLength < 0) ? ProcessMessageHeader() : ProcessMessageBody())
					{
					}
				}
			}
			catch (Exception ex)
			{
				IsRunning = false;
				HandleFatalError(ex);
			}
			finally
			{
				if (this.LogMessage != null)
				{
					Log(LogCategory.DebugAdapterOutput, rawData?.PeekString());
				}
			}
			IsRunning = false;
		}

		private void HandleFatalError(Exception ex)
		{
			lock (dispatcherSyncObject)
			{
				if (isHandlingError)
				{
					return;
				}
				isHandlingError = true;
			}
			if (!(ex is IOException) || !CancellationToken.IsCancellationRequested)
			{
				Log(LogCategory.Warning, "Stopping due to fatal error: " + ExceptionToString(ex));
				Stop();
				DispatcherErrorEventArgs e = new DispatcherErrorEventArgs(ex);
				this.DispatcherError?.Invoke(this, e);
			}
		}

		private static string ExceptionToString(Exception ex)
		{
			if (ex != null)
			{
				return ex.GetType().Name + ": " + ex.Message;
			}
			return string.Empty;
		}

		private bool ProcessMessageBody()
		{
			if (rawData.Length < nextMessageBodyLength)
			{
				return false;
			}
			string msg = rawData.PopString(nextMessageBodyLength);
			nextMessageBodyLength = -1;
			try
			{
				lock (dispatcherSyncObject)
				{
					queueEvents = true;
				}
				HandleIncomingMessage(msg);
				return true;
			}
			finally
			{
				lock (dispatcherSyncObject)
				{
					queueEvents = false;
				}
			}
		}

		private bool ProcessMessageHeader()
		{
			string text = rawData.PeekString();
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			Match match = ContentLengthMatcher.Match(text);
			if (!match.Success)
			{
				return false;
			}
			Log(LogCategory.DebugAdapterOutput, match.Groups["ignored"].Value?.Trim());
			nextMessageBodyLength = Convert.ToInt32(match.Groups["length"].Value, CultureInfo.InvariantCulture);
			rawData.RemoveData(ProtocolEncoding.GetByteCount(match.Value));
			return true;
		}

		private void HandleIncomingMessage(string msg)
		{
			ProtocolMessage protocolMessage = JsonConvert.DeserializeObject<ProtocolMessage>(msg, jsonSettings);
			if (protocolMessage != null)
			{
				switch (protocolMessage.Type)
				{
				case ProtocolMessage.TypeValue.Event:
					HandleIncomingEvent(msg);
					break;
				case ProtocolMessage.TypeValue.Request:
					HandleIncomingRequest(msg);
					break;
				case ProtocolMessage.TypeValue.Response:
					HandleIncomingResponse(msg);
					break;
				}
			}
		}

		private void HandleIncomingEvent(string msg)
		{
			ProtocolEvent protocolEvent = JsonConvert.DeserializeObject<ProtocolEvent>(msg, jsonSettings);
			TraceMessage(protocolEvent, msg, isSend: false);
			IEventInfo value = null;
			if (!eventRegistrations.TryGetValue(protocolEvent.EventType, out value) && !eventRegistrations.TryGetValue("*", out value))
			{
				Log(LogCategory.Warning, "No handler registered for event of type '{0}'!".FormatInvariantWithArgs(protocolEvent.EventType));
				return;
			}
			DebugEvent debugEvent = null;
			debugEvent = ((!((protocolEvent.Body != null) ? true : false)) ? ((DebugEvent)Activator.CreateInstance(value.EventType)) : ((DebugEvent)((JToken)protocolEvent.Body).ToObject(value.EventType, jsonSerializer)));
			debugEvent.Type = protocolEvent.EventType;
			value.InvokeHandler(debugEvent);
		}

		private void HandleIncomingRequest(string msg)
		{
			ProtocolRequest protocolRequest = JsonConvert.DeserializeObject<ProtocolRequest>(msg, jsonSettings);
			TraceMessage(protocolRequest, msg, isSend: false);
			if (!requestRegistrations.TryGetValue(protocolRequest.Command, out IRequestInfo value) && !requestRegistrations.TryGetValue("*", out value))
			{
				throw new InvalidOperationException("No handler registered for request of type '{0}'!".FormatInvariantWithArgs(protocolRequest.Command));
			}
			object args = null;
			if (protocolRequest.Arguments != null)
			{
				args = ((JToken)protocolRequest.Arguments).ToObject(value.ArgsType, jsonSerializer);
			}
			value.InvokeHandler(this, protocolRequest, args);
		}

		private void HandleIncomingResponse(string msg)
		{
			ProtocolResponse response = JsonConvert.DeserializeObject<ProtocolResponse>(msg, jsonSettings);
			bool allowCommandResponseNull = options.HasFlag(DebugProtocolOptions.AllowResponseCommandNull);
			IPendingRequest pendingRequest = null;
			lock (pendingRequests)
			{
				pendingRequest = pendingRequests.FirstOrDefault((IPendingRequest p) => p.Request.Seq == response.RequestSeq && ((allowCommandResponseNull && string.IsNullOrEmpty(response.Command)) || string.Equals(p.Request.Command, response.Command, responseCommandStringComparison)));
				if (pendingRequest != null)
				{
					pendingRequests.Remove(pendingRequest);
				}
			}
			TraceMessage(response, msg, isSend: false, pendingRequest?.Cancelled ?? false, (int?)pendingRequest?.ElapsedTime.TotalMilliseconds);
			if (pendingRequest == null)
			{
				string message = "Unable to locate a pending request with type '{0}' and sequence number '{1}'!".FormatInvariantWithArgs(response.Command, response.RequestSeq);
				Log(LogCategory.Warning, message);
				if (!options.HasFlag(DebugProtocolOptions.IgnoreUnexpectedResponses))
				{
					throw new InvalidOperationException(message);
				}
			}
			else
			{
				if (pendingRequest.Cancelled)
				{
					return;
				}
				if (response.Success)
				{
					pendingRequest.InvokeCompletionFunc(pendingRequest.Request.Arguments, response.Body, jsonSerializer);
					return;
				}
				Message detailMessage = null;
				if (response.Body is JObject)
				{
					ErrorResponse errorResponse = ((JObject)response.Body).ToObject<ErrorResponse>(jsonSerializer);
					if (errorResponse != null)
					{
						detailMessage = errorResponse.Error;
					}
				}
				pendingRequest.InvokeErrorFunc(pendingRequest.Request.Arguments, new ProtocolException(response.Message, detailMessage));
			}
		}

		internal void SendRequestCore<TArgs, TResponse>(IDebugRequestWithResponse<TArgs, TResponse> request, Action<TArgs, TResponse> completionFunc, Action<TArgs, ProtocolException> errorFunc = null) where TArgs : class, new()where TResponse : ResponseBody
		{
			SendRequestCore(new PendingRequestWithResponse<TArgs, TResponse>(this, new ProtocolRequest(GetNextSequenceNumber(), request.RequestType, request.Args), completionFunc, errorFunc));
		}

		internal TResponse SendRequestSyncCore<TArgs, TResponse>(IDebugRequestWithResponse<TArgs, TResponse> request) where TArgs : class, new()where TResponse : ResponseBody
		{
			VerifySynchronousOperationAllowed();
			TResponse syncResponse = null;
			ProtocolException error = null;
			AutoResetEvent evt = new AutoResetEvent(initialState: false);
			try
			{
				try
				{
					this.SynchronousOperationBegin?.Invoke(this, EventArgs.Empty);
					SendRequestCore(request, delegate(TArgs args, TResponse response)
					{
						syncResponse = response;
						SyncComplete(evt);
					}, delegate(TArgs args, ProtocolException ex)
					{
						error = ex;
						SyncComplete(evt);
					});
					WaitForSyncCompletion(evt);
				}
				finally
				{
					this.SynchronousOperationEnd?.Invoke(this, EventArgs.Empty);
				}
			}
			finally
			{
				if (evt != null)
				{
					((IDisposable)evt).Dispose();
				}
			}
			if (error != null)
			{
				throw error;
			}
			return syncResponse;
		}

		internal void SendRequestCore<TArgs>(IDebugRequest<TArgs> request, Action<TArgs> completionFunc, Action<TArgs, ProtocolException> errorFunc = null) where TArgs : class, new()
		{
			SendRequestCore(new PendingRequest<TArgs>(this, new ProtocolRequest(GetNextSequenceNumber(), request.RequestType, request.Args), completionFunc, errorFunc));
		}

		internal void SendRequestSyncCore<TArgs>(IDebugRequest<TArgs> request) where TArgs : class, new()
		{
			VerifySynchronousOperationAllowed();
			ProtocolException error = null;
			AutoResetEvent evt = new AutoResetEvent(initialState: false);
			try
			{
				try
				{
					this.SynchronousOperationBegin?.Invoke(this, EventArgs.Empty);
					SendRequestCore(request, delegate
					{
						SyncComplete(evt);
					}, delegate(TArgs args, ProtocolException ex)
					{
						error = ex;
						SyncComplete(evt);
					});
					WaitForSyncCompletion(evt);
				}
				finally
				{
					this.SynchronousOperationEnd?.Invoke(this, EventArgs.Empty);
				}
			}
			finally
			{
				if (evt != null)
				{
					((IDisposable)evt).Dispose();
				}
			}
			if (error != null)
			{
				throw error;
			}
		}

		private void SyncComplete(AutoResetEvent evt)
		{
			lock (tokenSource)
			{
				if (!CancellationToken.IsCancellationRequested)
				{
					evt.Set();
				}
			}
		}

		private void WaitForSyncCompletion(WaitHandle evt)
		{
			while (true)
			{
				switch (WaitHandle.WaitAny(new WaitHandle[2]
				{
					CancellationToken.WaitHandle,
					evt
				}, 100))
				{
				default:
					return;
				case 258:
					this.SynchronousOperationUpdate?.Invoke(this, EventArgs.Empty);
					break;
				case 0:
					throw new OperationCanceledException("Synchronous operation cancelled!");
				}
			}
		}

		private void SendRequestCore(IPendingRequest request)
		{
			lock (pendingRequests)
			{
				if (!IsRunning || CancellationToken.IsCancellationRequested)
				{
					Log(LogCategory.Warning, "Cannot send message when the protocol dispatcher is not running.");
					request.Cancel();
				}
				else
				{
					pendingRequests.Add(request);
					SendMessageCore(request.Request);
				}
			}
		}

		internal void SendEventCore(DebugEvent evt)
		{
			if (QueueEvents && !evt.Type.Equals("output", StringComparison.Ordinal))
			{
				queuedEvents.Enqueue(new ProtocolEvent(evt.Type, evt));
			}
			else
			{
				SendMessageCore(new ProtocolEvent(evt.Type, evt));
			}
		}

		internal void SendMessageCore(ProtocolMessage message)
		{
			if (message.Seq == 0)
			{
				message.Seq = GetNextSequenceNumber();
			}
			TraceMessage(message, null, isSend: true);
			string s = JsonConvert.SerializeObject(message, jsonSettings);
			byte[] bytes = ProtocolEncoding.GetBytes(s);
			string s2 = "Content-Length: {0}\r\n\r\n".FormatInvariantWithArgs(bytes.Length);
			byte[] bytes2 = ProtocolEncoding.GetBytes(s2);
			byte[] array = new byte[bytes2.Length + bytes.Length];
			Buffer.BlockCopy(bytes2, 0, array, 0, bytes2.Length);
			Buffer.BlockCopy(bytes, 0, array, bytes2.Length, bytes.Length);
			lock (outgoingSyncObj)
			{
				outgoingQueue.Enqueue(array);
				if (!isSendingMessages)
				{
					isSendingMessages = true;
					messagesPendingEvent.Reset();
					Task.Run(delegate
					{
						SendQueuedMessages();
					});
				}
			}
		}

		private void SendQueuedMessages()
		{
			while (!CancellationToken.IsCancellationRequested)
			{
				byte[] array;
				lock (outgoingSyncObj)
				{
					if (outgoingQueue.Count == 0)
					{
						isSendingMessages = false;
						messagesPendingEvent.Set();
						return;
					}
					array = outgoingQueue.Dequeue();
				}
				try
				{
					writeStream.Write(array, 0, array.Length);
					writeStream.Flush();
				}
				catch (Exception ex)
				{
					lock (outgoingSyncObj)
					{
						isSendingMessages = false;
						messagesPendingEvent.Set();
					}
					HandleFatalError(ex);
					return;
				}
			}
			lock (outgoingSyncObj)
			{
				isSendingMessages = false;
				messagesPendingEvent.Set();
			}
		}
	}
}
