using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public enum CompletionItemType
	{
		[EnumMember(Value = "method")]
		Method = 0,
		[EnumMember(Value = "function")]
		Function = 1,
		[EnumMember(Value = "constructor")]
		Constructor = 2,
		[EnumMember(Value = "field")]
		Field = 3,
		[EnumMember(Value = "variable")]
		Variable = 4,
		[EnumMember(Value = "class")]
		Class = 5,
		[EnumMember(Value = "interface")]
		Interface = 6,
		[EnumMember(Value = "module")]
		Module = 7,
		[EnumMember(Value = "property")]
		Property = 8,
		[EnumMember(Value = "unit")]
		Unit = 9,
		[EnumMember(Value = "value")]
		Value = 10,
		[EnumMember(Value = "enum")]
		Enum = 11,
		[EnumMember(Value = "keyword")]
		Keyword = 12,
		[EnumMember(Value = "snippet")]
		Snippet = 13,
		[EnumMember(Value = "text")]
		Text = 14,
		[EnumMember(Value = "color")]
		Color = 0xF,
		[EnumMember(Value = "file")]
		File = 0x10,
		[EnumMember(Value = "reference")]
		Reference = 17,
		[EnumMember(Value = "customcolor")]
		Customcolor = 18,
		[DefaultEnumValue]
		Unknown = int.MaxValue
	}
}
