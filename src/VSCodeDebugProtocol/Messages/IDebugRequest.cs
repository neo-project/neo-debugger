namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    internal interface IDebugRequest<TArgs> where TArgs : class, new()
    {
        string RequestType
        {
            get;
        }

        TArgs Args
        {
            get;
        }
    }
}
