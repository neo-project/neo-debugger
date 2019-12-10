using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class LoadSymbolsRequest : DebugRequest<LoadSymbolsArguments>
    {
        public const string RequestType = "loadSymbols";

        [JsonIgnore]
        public object Id
        {
            get
            {
                return base.Args.Id;
            }
            set
            {
                base.Args.Id = value;
            }
        }

        public LoadSymbolsRequest()
            : base("loadSymbols")
        {
        }

        public LoadSymbolsRequest(object id)
            : base("loadSymbols")
        {
            base.Args.Id = id;
        }
    }
}
