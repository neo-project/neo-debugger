using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
    public class LogEventArgs : EventArgs
    {
        public LogCategory Category
        {
            get;
        }

        public string Message
        {
            get;
        }

        internal LogEventArgs(LogCategory category, string message)
        {
            Category = category;
            Message = message;
        }
    }
}
