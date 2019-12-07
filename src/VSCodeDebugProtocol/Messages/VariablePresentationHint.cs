using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class VariablePresentationHint : DebugType
    {
        public enum KindValue
        {
            [EnumMember(Value = "property")]
            Property = 0,
            [EnumMember(Value = "method")]
            Method = 1,
            [EnumMember(Value = "class")]
            Class = 2,
            [EnumMember(Value = "data")]
            Data = 3,
            [EnumMember(Value = "event")]
            Event = 4,
            [EnumMember(Value = "baseClass")]
            BaseClass = 5,
            [EnumMember(Value = "innerClass")]
            InnerClass = 6,
            [EnumMember(Value = "interface")]
            Interface = 7,
            [EnumMember(Value = "mostDerivedClass")]
            MostDerivedClass = 8,
            [EnumMember(Value = "virtual")]
            Virtual = 9,
            [EnumMember(Value = "dataBreakpoint")]
            DataBreakpoint = 10,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        [Flags]
        public enum AttributesValue : uint
        {
            None = 0x0,
            [EnumMember(Value = "static")]
            Static = 0x1,
            [EnumMember(Value = "constant")]
            Constant = 0x2,
            [EnumMember(Value = "readOnly")]
            ReadOnly = 0x4,
            [EnumMember(Value = "rawString")]
            RawString = 0x8,
            [EnumMember(Value = "hasObjectId")]
            HasObjectId = 0x10,
            [EnumMember(Value = "canHaveObjectId")]
            CanHaveObjectId = 0x20,
            [EnumMember(Value = "hasSideEffects")]
            HasSideEffects = 0x40,
            [EnumMember(Value = "failedEvaluation")]
            FailedEvaluation = 0x80
        }

        public enum VisibilityValue
        {
            [EnumMember(Value = "public")]
            Public = 0,
            [EnumMember(Value = "private")]
            Private = 1,
            [EnumMember(Value = "protected")]
            Protected = 2,
            [EnumMember(Value = "internal")]
            Internal = 3,
            [EnumMember(Value = "final")]
            Final = 4,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        [JsonProperty("kind", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private NullableEnumValue<KindValue> _kind = new NullableEnumValue<KindValue>();

        [JsonProperty("attributes", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private NullableFlagsEnumValue<AttributesValue> _attributes = new NullableFlagsEnumValue<AttributesValue>();

        [JsonProperty("visibility", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private NullableEnumValue<VisibilityValue> _visibility = new NullableEnumValue<VisibilityValue>();

        [JsonIgnore]
        public KindValue? Kind
        {
            get
            {
                return _kind.Value;
            }
            set
            {
                _kind.Value = value;
            }
        }

        [JsonIgnore]
        public AttributesValue? Attributes
        {
            get
            {
                return _attributes.Value;
            }
            set
            {
                _attributes.Value = value;
            }
        }

        [JsonIgnore]
        public VisibilityValue? Visibility
        {
            get
            {
                return _visibility.Value;
            }
            set
            {
                _visibility.Value = value;
            }
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public VariablePresentationHint(KindValue? kind = null, AttributesValue? attributes = null, VisibilityValue? visibility = null)
        {
            Kind = kind;
            Attributes = attributes;
            Visibility = visibility;
        }

        public VariablePresentationHint()
        {
        }
    }
}
