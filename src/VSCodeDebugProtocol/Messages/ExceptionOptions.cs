using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ExceptionOptions : DebugType
    {
        [JsonProperty("breakMode")]
        private EnumValue<ExceptionBreakMode> _breakMode = new EnumValue<ExceptionBreakMode>();

        [JsonProperty("path", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<ExceptionPathSegment> Path
        {
            get;
            set;
        }

        [JsonIgnore]
        public ExceptionBreakMode BreakMode
        {
            get
            {
                return _breakMode.Value;
            }
            set
            {
                _breakMode.Value = value;
            }
        }

        [JsonProperty("conditions", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<ExceptionCondition> Conditions
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public ExceptionOptions(ExceptionBreakMode breakMode, List<ExceptionPathSegment> path = null, List<ExceptionCondition> conditions = null)
        {
            Path = path;
            BreakMode = breakMode;
            Conditions = conditions;
        }

        public ExceptionOptions()
        {
            Path = new List<ExceptionPathSegment>();
            Conditions = new List<ExceptionCondition>();
        }

        public ExceptionOptions(ExceptionBreakMode breakMode)
        {
            BreakMode = breakMode;
        }

        public ExceptionOptions(ExceptionBreakMode breakMode, params ExceptionPathSegment[] path)
            : this(breakMode)
        {
            Path = path?.ToList();
        }
    }
}
