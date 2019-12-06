using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
	internal class PendingRequestWithResponse<TArgs, TResponse> : PendingRequestBase where TArgs : class where TResponse : class
	{
		private Action<TArgs, TResponse> completionFunc;

		private Action<TArgs, ProtocolException> errorFunc;

		internal PendingRequestWithResponse(DebugProtocol protocol, ProtocolRequest request, Action<TArgs, TResponse> completionFunc, Action<TArgs, ProtocolException> errorFunc = null)
			: base(protocol, request)
		{
			this.completionFunc = completionFunc;
			this.errorFunc = errorFunc;
		}

		protected override void InvokeCompletionFuncCore(object args, JToken responseBody, JsonSerializer serializer)
		{
			try
			{
				completionFunc?.Invoke((TArgs)args, (responseBody != null) ? responseBody.ToObject<TResponse>(serializer) : null);
			}
			catch (JsonSerializationException)
			{
				throw;
			}
		}

		protected override void InvokeErrorFuncCore(object args, ProtocolException ex)
		{
			if (errorFunc != null)
			{
				errorFunc((TArgs)args, ex);
				return;
			}
			throw ex;
		}
	}
}
