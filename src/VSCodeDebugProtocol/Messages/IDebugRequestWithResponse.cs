namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    internal interface IDebugRequestWithResponse<TArgs, TResponse> : IDebugRequest<TArgs> where TArgs : class, new() where TResponse : ResponseBody
    {
    }
}
