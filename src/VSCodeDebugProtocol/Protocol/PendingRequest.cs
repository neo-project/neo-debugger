using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
	internal class PendingRequest<TArgs> : PendingRequestBase
	{
		private Action<TArgs> completionFunc;

		private Action<TArgs, ProtocolException> errorFunc;

		internal PendingRequest(DebugProtocol protocol, ProtocolRequest request, Action<TArgs> completionFunc, Action<TArgs, ProtocolException> errorFunc = null)
			: base(protocol, request)
		{
			this.completionFunc = completionFunc;
			this.errorFunc = errorFunc;
		}

		protected override void InvokeCompletionFuncCore(object args, JToken responseBody, JsonSerializer serializer)
		{
			completionFunc?.Invoke((TArgs)args);
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
