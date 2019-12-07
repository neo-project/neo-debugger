namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public abstract class DebugRequestWithResponse<TArgs, TResponse> : DebugRequest<TArgs>, IDebugRequestWithResponse<TArgs, TResponse>, IDebugRequest<TArgs> where TArgs : class, new() where TResponse : ResponseBody
    {
        protected DebugRequestWithResponse(string command)
            : base(command)
        {
        }
    }
}
