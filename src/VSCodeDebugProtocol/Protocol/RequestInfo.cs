using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
	internal class RequestInfo<TArgs> : IRequestInfo where TArgs : class, new()
	{
		private Action<IRequestResponder<TArgs>> handler;

		public Type ArgsType => typeof(TArgs);

		internal RequestInfo(Action<IRequestResponder<TArgs>> handler)
		{
			this.handler = handler;
		}

		void IRequestInfo.InvokeHandler(DebugProtocol protocol, ProtocolRequest request, object args)
		{
			try
			{
				IRequestResponder<TArgs> obj = new RequestResponder<TArgs>(protocol, request.Command, request.Seq, (TArgs)args);
				handler(obj);
			}
			finally
			{
				protocol.ProcessQueuedEvents();
			}
		}
	}
}
