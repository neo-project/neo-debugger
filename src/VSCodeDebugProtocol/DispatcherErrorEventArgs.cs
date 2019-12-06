using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
	public class DispatcherErrorEventArgs : EventArgs
	{
		public Exception Exception
		{
			get;
			private set;
		}

		internal DispatcherErrorEventArgs(Exception ex)
		{
			Exception = ex;
		}
	}
}
