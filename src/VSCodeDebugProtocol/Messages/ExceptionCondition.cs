using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ExceptionCondition : DebugType
    {
        public enum TypeValue
        {
            [EnumMember(Value = "module")]
            Module = 0,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        public enum OperatorValue
        {
            [EnumMember(Value = "equals")]
            Equals = 0,
            [EnumMember(Value = "notEquals")]
            NotEquals = 1,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        [JsonProperty("type")]
        private EnumValue<TypeValue> _type = new EnumValue<TypeValue>();

        [JsonProperty("operator", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private NullableEnumValue<OperatorValue> _operator = new NullableEnumValue<OperatorValue>();

        [JsonIgnore]
        public TypeValue Type
        {
            get
            {
                return _type.Value;
            }
            set
            {
                _type.Value = value;
            }
        }

        [JsonIgnore]
        public OperatorValue? Operator
        {
            get
            {
                return _operator.Value;
            }
            set
            {
                _operator.Value = value;
            }
        }

        [JsonProperty("value")]
        public string Value
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public ExceptionCondition(TypeValue type, string value, OperatorValue? @operator = null)
        {
            Type = type;
            Operator = @operator;
            Value = value;
        }

        public ExceptionCondition()
        {
        }

        public ExceptionCondition(TypeValue type, string value)
        {
            Type = type;
            Value = value;
        }
    }
}
