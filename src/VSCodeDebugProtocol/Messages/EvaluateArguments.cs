using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class EvaluateArguments : DebugRequestArguments
	{
		public enum ContextValue
		{
			[EnumMember(Value = "watch")]
			Watch = 0,
			[EnumMember(Value = "repl")]
			Repl = 1,
			[EnumMember(Value = "hover")]
			Hover = 2,
			[DefaultEnumValue]
			Unknown = int.MaxValue
		}

		[JsonProperty("context", DefaultValueHandling = DefaultValueHandling.Ignore)]
		private NullableEnumValue<ContextValue> _context = new NullableEnumValue<ContextValue>();

		[JsonProperty("expression")]
		public string Expression
		{
			get;
			set;
		}

		[JsonProperty("frameId", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? FrameId
		{
			get;
			set;
		}

		[JsonIgnore]
		public ContextValue? Context
		{
			get
			{
				return _context.Value;
			}
			set
			{
				_context.Value = value;
			}
		}

		[JsonProperty("format", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public ValueFormat Format
		{
			get;
			set;
		}

		[JsonProperty("noSideEffects", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? NoSideEffects
		{
			get;
			set;
		}

		[JsonProperty("noFuncEval", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? NoFuncEval
		{
			get;
			set;
		}

		[JsonProperty("timeout", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Timeout
		{
			get;
			set;
		}
	}
}
