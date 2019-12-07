using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
    public interface IRequestResponder
    {
        object Arguments
        {
            get;
        }

        string Command
        {
            get;
        }

        void SetError(ProtocolException exception);

        void SetResponse(ResponseBody response);
    }
    public interface IRequestResponder<TArgs> : IRequestResponder where TArgs : class, new()
    {
        new TArgs Arguments
        {
            get;
        }
    }
    public interface IRequestResponder<TArgs, TResponse> : IRequestResponder<TArgs>, IRequestResponder where TArgs : class, new() where TResponse : ResponseBody
    {
        void SetResponse(TResponse response);
    }
}
