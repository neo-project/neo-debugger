using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SymbolOptionsModuleFilter : DebugType
    {
        public enum ModeValue
        {
            [EnumMember(Value = "loadAllButExcluded")]
            LoadAllButExcluded = 0,
            [EnumMember(Value = "loadOnlyIncluded")]
            LoadOnlyIncluded = 1,
            [DefaultEnumValue]
            Unknown = int.MaxValue
        }

        [JsonProperty("mode")]
        private EnumValue<ModeValue> _mode = new EnumValue<ModeValue>();

        [JsonIgnore]
        public ModeValue Mode
        {
            get
            {
                return _mode.Value;
            }
            set
            {
                _mode.Value = value;
            }
        }

        [JsonProperty("excludedModules", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> ExcludedModules
        {
            get;
            set;
        }

        [JsonProperty("includedModules", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> IncludedModules
        {
            get;
            set;
        }

        [JsonProperty("includeSymbolsNextToModules", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? IncludeSymbolsNextToModules
        {
            get;
            set;
        }

        public SymbolOptionsModuleFilter()
        {
            ExcludedModules = new List<string>();
            IncludedModules = new List<string>();
        }

        public SymbolOptionsModuleFilter(ModeValue mode)
        {
            Mode = mode;
        }
    }
}
