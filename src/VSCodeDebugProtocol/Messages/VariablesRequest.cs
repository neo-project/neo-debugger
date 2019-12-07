using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class VariablesRequest : DebugRequestWithResponse<VariablesArguments, VariablesResponse>
    {
        public const string RequestType = "variables";

        [JsonIgnore]
        public int VariablesReference
        {
            get
            {
                return base.Args.VariablesReference;
            }
            set
            {
                base.Args.VariablesReference = value;
            }
        }

        [JsonIgnore]
        public VariablesArguments.FilterValue? Filter
        {
            get
            {
                return base.Args.Filter;
            }
            set
            {
                base.Args.Filter = value;
            }
        }

        [JsonIgnore]
        public int? Start
        {
            get
            {
                return base.Args.Start;
            }
            set
            {
                base.Args.Start = value;
            }
        }

        [JsonIgnore]
        public int? Count
        {
            get
            {
                return base.Args.Count;
            }
            set
            {
                base.Args.Count = value;
            }
        }

        [JsonIgnore]
        public ValueFormat Format
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

        [JsonIgnore]
        public int? Timeout
        {
            get
            {
                return base.Args.Timeout;
            }
            set
            {
                base.Args.Timeout = value;
            }
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public VariablesRequest(int variablesReference, VariablesArguments.FilterValue? filter = null, int? start = null, int? count = null, ValueFormat format = null, int? timeout = null)
            : base("variables")
        {
            base.Args.VariablesReference = variablesReference;
            base.Args.Filter = filter;
            base.Args.Start = start;
            base.Args.Count = count;
            base.Args.Format = format;
            base.Args.Timeout = timeout;
        }

        public VariablesRequest()
            : base("variables")
        {
        }

        public VariablesRequest(int variablesReference)
            : base("variables")
        {
            base.Args.VariablesReference = variablesReference;
        }
    }
}
