using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
	public class ResponseTimeThresholdExceededEventArgs : EventArgs
	{
		public string Command
		{
			get;
		}

		public int SequenceId
		{
			get;
		}

		public int Threshold
		{
			get;
		}

		internal ResponseTimeThresholdExceededEventArgs(string command, int seq, int threshold)
		{
			Command = command;
			SequenceId = seq;
			Threshold = threshold;
		}
	}
}
