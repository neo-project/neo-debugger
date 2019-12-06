using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class StackTraceRequest : DebugRequestWithResponse<StackTraceArguments, StackTraceResponse>
	{
		public const string RequestType = "stackTrace";

		[JsonIgnore]
		public int ThreadId
		{
			get
			{
				return base.Args.ThreadId;
			}
			set
			{
				base.Args.ThreadId = value;
			}
		}

		[JsonIgnore]
		public int? StartFrame
		{
			get
			{
				return base.Args.StartFrame;
			}
			set
			{
				base.Args.StartFrame = value;
			}
		}

		[JsonIgnore]
		public int? Levels
		{
			get
			{
				return base.Args.Levels;
			}
			set
			{
				base.Args.Levels = value;
			}
		}

		[JsonIgnore]
		public StackFrameFormat Format
		{
			get
			{
				return base.Args.Format;
			}
			set
			{
				base.Args.Format = value;
			}
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public StackTraceRequest(int threadId, int? startFrame = null, int? levels = null, StackFrameFormat format = null)
			: base("stackTrace")
		{
			base.Args.ThreadId = threadId;
			base.Args.StartFrame = startFrame;
			base.Args.Levels = levels;
			base.Args.Format = format;
		}

		public StackTraceRequest()
			: base("stackTrace")
		{
		}

		public StackTraceRequest(int threadId)
			: base("stackTrace")
		{
			base.Args.ThreadId = threadId;
		}
	}
}
