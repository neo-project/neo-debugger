using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
	internal abstract class PendingRequestBase : IPendingRequest
	{
		private const int SlowRequestThreshold = 1000;

		private DebugProtocol protocol;

		private DateTime startTime;

		private object timerLock = new object();

		private Timer timer;

		public ProtocolRequest Request
		{
			get;
		}

		public bool Cancelled
		{
			get;
			protected set;
		}

		public TimeSpan ElapsedTime => DateTime.Now - startTime;

		internal PendingRequestBase(DebugProtocol protocol, ProtocolRequest request)
		{
			startTime = DateTime.Now;
			this.protocol = protocol;
			Request = request;
			timer = new Timer(delegate
			{
				CancelTimer();
				this.protocol.NotifyResponseTimeThresholdExceeded(new ResponseTimeThresholdExceededEventArgs(Request.Command, Request.Seq, 1000));
			}, null, 1000, -1);
		}

		public void InvokeCompletionFunc(object args, JToken responseBody, JsonSerializer serializer)
		{
			NotifyCompletion(succeeded: true);
			InvokeCompletionFuncCore(args, responseBody, serializer);
		}

		public void InvokeErrorFunc(object args, ProtocolException ex)
		{
			NotifyCompletion(succeeded: false);
			InvokeErrorFuncCore(args, ex);
		}

		public void Cancel()
		{
			if (!Cancelled)
			{
				Cancelled = true;
				this.InvokeErrorFunc(Request.Arguments, new ProtocolException("Operation cancelled", new OperationCanceledException()));
			}
		}

		private void CancelTimer()
		{
			lock (timerLock)
			{
				if (timer != null)
				{
					timer.Dispose();
					timer = null;
				}
			}
		}

		private void NotifyCompletion(bool succeeded)
		{
			CancelTimer();
			protocol.NotifyRequestCompletion(new RequestCompletedEventArgs(Request.Command, Request.Seq, ElapsedTime, (!succeeded) ? ((!Cancelled) ? RequestCompletionStatus.Failed : RequestCompletionStatus.Cancelled) : RequestCompletionStatus.Succeeded));
		}

		protected abstract void InvokeCompletionFuncCore(object args, JToken responseBody, JsonSerializer serializer);

		protected abstract void InvokeErrorFuncCore(object args, ProtocolException ex);
	}
}
