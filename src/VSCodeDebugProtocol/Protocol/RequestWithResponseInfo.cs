using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
    internal class RequestWithResponseInfo<TArgs, TResponse> : IRequestInfo where TArgs : class, new() where TResponse : ResponseBody
    {
        private Action<IRequestResponder<TArgs, TResponse>> handler;

        public Type ArgsType => typeof(TArgs);

        internal RequestWithResponseInfo(Action<IRequestResponder<TArgs, TResponse>> handler)
        {
            this.handler = handler;
        }

        public void InvokeHandler(DebugProtocol protocol, ProtocolRequest request, object args)
        {
            try
            {
                IRequestResponder<TArgs, TResponse> obj = new RequestResponder<TArgs, TResponse>(protocol, request.Command, request.Seq, (TArgs)args);
                handler(obj);
            }
            finally
            {
                protocol.ProcessQueuedEvents();
            }
        }
    }
}
