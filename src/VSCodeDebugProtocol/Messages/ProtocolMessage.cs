using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ProtocolMessage
	{
		public enum TypeValue
		{
			[EnumMember(Value = "request")]
			Request = 0,
			[EnumMember(Value = "response")]
			Response = 1,
			[EnumMember(Value = "event")]
			Event = 2,
			[DefaultEnumValue]
			Unknown = int.MaxValue
		}

		[JsonProperty("type")]
		private EnumValue<TypeValue> _type = new EnumValue<TypeValue>();

		[JsonProperty("seq")]
		public int Seq
		{
			get;
			set;
		}

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

		public ProtocolMessage(TypeValue type)
		{
			Type = type;
		}
	}
}
