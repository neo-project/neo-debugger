using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class CompletionItem : DebugType
    {
        [JsonProperty("type", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private NullableEnumValue<CompletionItemType> _type = new NullableEnumValue<CompletionItemType>();

        [JsonProperty("label")]
        public string Label
        {
            get;
            set;
        }

        [JsonProperty("text", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Text
        {
            get;
            set;
        }

        [JsonIgnore]
        public CompletionItemType? Type
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

        [JsonProperty("start", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Start
        {
            get;
            set;
        }

        [JsonProperty("length", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? Length
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public CompletionItem(string label, string text = null, CompletionItemType? type = null, int? start = null, int? length = null)
        {
            Label = label;
            Text = text;
            Type = type;
            Start = start;
            Length = length;
        }

        public CompletionItem()
        {
        }

        public CompletionItem(string label)
        {
            Label = label;
        }
    }
}
