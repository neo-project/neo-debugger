using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
    public class RequestCompletedEventArgs : EventArgs
    {
        public string Command
        {
            get;
        }

        public int SequenceId
        {
            get;
        }

        public TimeSpan ElapsedTime
        {
            get;
        }

        public RequestCompletionStatus Status
        {
            get;
        }

        internal RequestCompletedEventArgs(string command, int seq, TimeSpan elapsedTime, RequestCompletionStatus status)
        {
            Command = command;
            SequenceId = seq;
            ElapsedTime = elapsedTime;
            Status = status;
        }
    }
}
