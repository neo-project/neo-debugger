namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public abstract class DebugClientRequestWithResponse<TArgs, TResponse> : DebugClientRequest<TArgs>, IDebugRequestWithResponse<TArgs, TResponse>, IDebugRequest<TArgs> where TArgs : class, new() where TResponse : ResponseBody
    {
        protected DebugClientRequestWithResponse(string command)
            : base(command)
        {
        }
    }
}
