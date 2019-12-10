using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ExceptionInfoRequest : DebugRequestWithResponse<ExceptionInfoArguments, ExceptionInfoResponse>
    {
        public const string RequestType = "exceptionInfo";

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

        public ExceptionInfoRequest()
            : base("exceptionInfo")
        {
        }

        public ExceptionInfoRequest(int threadId)
            : base("exceptionInfo")
        {
            base.Args.ThreadId = threadId;
        }
    }
}
