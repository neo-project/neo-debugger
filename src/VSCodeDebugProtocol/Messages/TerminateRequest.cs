using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class TerminateRequest : DebugRequest<TerminateArguments>
    {
        public const string RequestType = "terminate";

        [JsonIgnore]
        public bool? Restart
        {
            get
            {
                return base.Args.Restart;
            }
            set
            {
                base.Args.Restart = value;
            }
        }

        public TerminateRequest()
            : base("terminate")
        {
        }
    }
}
