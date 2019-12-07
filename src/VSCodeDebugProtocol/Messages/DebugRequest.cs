namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public abstract class DebugRequest<TArgs> : IDebugRequest<TArgs> where TArgs : class, new()
    {
        internal string Command
        {
            get;
            private set;
        }

        string IDebugRequest<TArgs>.RequestType => Command;

        public TArgs Args
        {
            get;
            private set;
        }

        protected DebugRequest(string command)
        {
            Command = command;
            Args = new TArgs();
        }
    }
}
