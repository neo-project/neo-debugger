using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetExceptionBreakpointsRequest : DebugRequest<SetExceptionBreakpointsArguments>
    {
        public const string RequestType = "setExceptionBreakpoints";

        [JsonIgnore]
        public List<string> Filters
        {
            get
            {
                return base.Args.Filters;
            }
            set
            {
                base.Args.Filters = value;
            }
        }

        [JsonIgnore]
        public List<ExceptionOptions> ExceptionOptions
        {
            get
            {
                return base.Args.ExceptionOptions;
            }
            set
            {
                base.Args.ExceptionOptions = value;
            }
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public SetExceptionBreakpointsRequest(List<string> filters, List<ExceptionOptions> exceptionOptions = null)
            : base("setExceptionBreakpoints")
        {
            base.Args.Filters = filters;
            base.Args.ExceptionOptions = exceptionOptions;
        }

        public SetExceptionBreakpointsRequest()
            : base("setExceptionBreakpoints")
        {
            base.Args.Filters = new List<string>();
            base.Args.ExceptionOptions = new List<ExceptionOptions>();
        }

        public SetExceptionBreakpointsRequest(List<string> filters)
            : base("setExceptionBreakpoints")
        {
            base.Args.Filters = filters;
        }
    }
}
