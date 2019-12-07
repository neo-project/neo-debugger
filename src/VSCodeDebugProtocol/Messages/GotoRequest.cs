using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class GotoRequest : DebugRequest<GotoArguments>
    {
        public const string RequestType = "goto";

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
        public int TargetId
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

        public GotoRequest()
            : base("goto")
        {
        }

        public GotoRequest(int threadId, int targetId)
            : base("goto")
        {
            base.Args.ThreadId = threadId;
            base.Args.TargetId = targetId;
        }
    }
}
