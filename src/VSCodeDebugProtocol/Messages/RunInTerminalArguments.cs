using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class RunInTerminalArguments : DebugRequestArguments
	{
		public enum KindValue
		{
			[EnumMember(Value = "integrated")]
			Integrated = 0,
			[EnumMember(Value = "external")]
			External = 1,
			[DefaultEnumValue]
			Unknown = int.MaxValue
		}

		[JsonProperty("kind", DefaultValueHandling = DefaultValueHandling.Ignore)]
		private NullableEnumValue<KindValue> _kind = new NullableEnumValue<KindValue>();

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

		[JsonProperty("title", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Title
		{
			get;
			set;
		}

		[JsonProperty("cwd")]
		public string Cwd
		{
			get;
			set;
		}

		[JsonProperty("args")]
		public List<string> Args
		{
			get;
			set;
		}

		[JsonProperty("env", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public Dictionary<string, object> Env
		{
			get;
			set;
		}
	}
}
