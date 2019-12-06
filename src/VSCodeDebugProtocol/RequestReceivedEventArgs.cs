using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
	public class RequestReceivedEventArgs : EventArgs
	{
		public string Command
		{
			get;
			private set;
		}

		public object Args
		{
			get;
			private set;
		}

		public ResponseBody Response
		{
			get;
			set;
		}

		internal RequestReceivedEventArgs(string command, object args)
		{
			Command = command;
			Args = args;
		}
	}
}
