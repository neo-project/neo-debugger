using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class InitializeArguments : DebugRequestArguments
    {
        public enum PathFormatValue
        {
            [EnumMember(Value = "path")]
            Path = 0,
            [EnumMember(Value = "uri")]
            Uri = 1,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        [JsonProperty("pathFormat", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private NullableEnumValue<PathFormatValue> _pathFormat = new NullableEnumValue<PathFormatValue>();

        [JsonProperty("clientID", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ClientID
        {
            get;
            set;
        }

        [JsonProperty("clientName", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ClientName
        {
            get;
            set;
        }

        [JsonProperty("adapterID")]
        public string AdapterID
        {
            get;
            set;
        }

        [JsonProperty("locale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Locale
        {
            get;
            set;
        }

        [JsonProperty("linesStartAt1", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? LinesStartAt1
        {
            get;
            set;
        }

        [JsonProperty("columnsStartAt1", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? ColumnsStartAt1
        {
            get;
            set;
        }

        [JsonIgnore]
        public PathFormatValue? PathFormat
        {
            get
            {
                return _pathFormat.Value;
            }
            set
            {
                _pathFormat.Value = value;
            }
        }

        [JsonProperty("supportsVariableType", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? SupportsVariableType
        {
            get;
            set;
        }

        [JsonProperty("supportsVariablePaging", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? SupportsVariablePaging
        {
            get;
            set;
        }

        [JsonProperty("supportsRunInTerminalRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? SupportsRunInTerminalRequest
        {
            get;
            set;
        }

        [JsonProperty("supportsMemoryReferences", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? SupportsMemoryReferences
        {
            get;
            set;
        }

        [JsonProperty("supportsHandshakeRequest", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? SupportsHandshakeRequest
        {
            get;
            set;
        }

        [JsonExtensionData(ReadData = true, WriteData = true)]
        public new Dictionary<string, JToken> AdditionalProperties
        {
            get
            {
                return base.AdditionalProperties;
            }
            set
            {
                base.AdditionalProperties = value;
            }
        }
    }
}
