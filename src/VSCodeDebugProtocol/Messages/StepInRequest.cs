using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class StepInRequest : DebugRequest<StepInArguments>
	{
		public const string RequestType = "stepIn";

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
		public int? TargetId
		{
			get
			{
				return base.Args.TargetId;
			}
			set
			{
				base.Args.TargetId = value;
			}
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public StepInRequest(int threadId, int? targetId = null)
			: base("stepIn")
		{
			base.Args.ThreadId = threadId;
			base.Args.TargetId = targetId;
		}

		public StepInRequest()
			: base("stepIn")
		{
		}

		public StepInRequest(int threadId)
			: base("stepIn")
		{
			base.Args.ThreadId = threadId;
		}
	}
}
