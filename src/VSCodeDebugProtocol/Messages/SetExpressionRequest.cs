using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SetExpressionRequest : DebugRequestWithResponse<SetExpressionArguments, SetExpressionResponse>
    {
        public const string RequestType = "setExpression";

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
        public SetExpressionRequest(string expression, string value, ValueFormat format = null, int? frameId = null, int? timeout = null)
            : base("setExpression")
        {
            base.Args.Expression = expression;
            base.Args.Value = value;
            base.Args.Format = format;
            base.Args.FrameId = frameId;
            base.Args.Timeout = timeout;
        }

        public SetExpressionRequest()
            : base("setExpression")
        {
        }

        public SetExpressionRequest(string expression, string value)
            : base("setExpression")
        {
            base.Args.Expression = expression;
            base.Args.Value = value;
        }
    }
}
