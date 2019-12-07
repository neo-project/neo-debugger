using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class EvaluateRequest : DebugRequestWithResponse<EvaluateArguments, EvaluateResponse>
    {
        public const string RequestType = "evaluate";

        [JsonIgnore]
        public string Expression
        {
            get
            {
                return base.Args.Expression;
            }
            set
            {
                base.Args.Expression = value;
            }
        }

        [JsonIgnore]
        public int? FrameId
        {
            get
            {
                return base.Args.FrameId;
            }
            set
            {
                base.Args.FrameId = value;
            }
        }

        [JsonIgnore]
        public EvaluateArguments.ContextValue? Context
        {
            get
            {
                return base.Args.Context;
            }
            set
            {
                base.Args.Context = value;
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
        public bool? NoSideEffects
        {
            get
            {
                return base.Args.NoSideEffects;
            }
            set
            {
                base.Args.NoSideEffects = value;
            }
        }

        [JsonIgnore]
        public bool? NoFuncEval
        {
            get
            {
                return base.Args.NoFuncEval;
            }
            set
            {
                base.Args.NoFuncEval = value;
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
        public EvaluateRequest(string expression, int? frameId = null, EvaluateArguments.ContextValue? context = null, ValueFormat format = null, bool? noSideEffects = null, bool? noFuncEval = null, int? timeout = null)
            : base("evaluate")
        {
            base.Args.Expression = expression;
            base.Args.FrameId = frameId;
            base.Args.Context = context;
            base.Args.Format = format;
            base.Args.NoSideEffects = noSideEffects;
            base.Args.NoFuncEval = noFuncEval;
            base.Args.Timeout = timeout;
        }

        public EvaluateRequest()
            : base("evaluate")
        {
        }

        public EvaluateRequest(string expression)
            : base("evaluate")
        {
            base.Args.Expression = expression;
        }
    }
}
