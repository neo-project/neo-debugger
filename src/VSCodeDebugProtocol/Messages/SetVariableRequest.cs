using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetVariableRequest : DebugRequestWithResponse<SetVariableArguments, SetVariableResponse>
    {
        public const string RequestType = "setVariable";

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
        public string Name
        {
            get
            {
                return base.Args.Name;
            }
            set
            {
                base.Args.Name = value;
            }
        }

        [JsonIgnore]
        public string Value
        {
            get
            {
                return base.Args.Value;
            }
            set
            {
                base.Args.Value = value;
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
        public SetVariableRequest(int variablesReference, string name, string value, ValueFormat format = null, int? timeout = null)
            : base("setVariable")
        {
            base.Args.VariablesReference = variablesReference;
            base.Args.Name = name;
            base.Args.Value = value;
            base.Args.Format = format;
            base.Args.Timeout = timeout;
        }

        public SetVariableRequest()
            : base("setVariable")
        {
        }

        public SetVariableRequest(int variablesReference, string name, string value)
            : base("setVariable")
        {
            base.Args.VariablesReference = variablesReference;
            base.Args.Name = name;
            base.Args.Value = value;
        }
    }
}
