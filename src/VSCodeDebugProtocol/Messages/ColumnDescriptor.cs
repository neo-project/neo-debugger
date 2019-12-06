using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ColumnDescriptor : DebugType, INamed
	{
		public enum TypeValue
		{
			[EnumMember(Value = "string")]
			String = 0,
			[EnumMember(Value = "number")]
			Number = 1,
			[EnumMember(Value = "boolean")]
			Boolean = 2,
			[EnumMember(Value = "unixTimestampUTC")]
			UnixTimestampUTC = 3,
			[DefaultEnumValue]
			Unknown = int.MaxValue
		}

		[JsonProperty("type", DefaultValueHandling = DefaultValueHandling.Ignore)]
		private NullableEnumValue<TypeValue> _type = new NullableEnumValue<TypeValue>();

		[JsonProperty("attributeName")]
		public string AttributeName
		{
			get;
			set;
		}

		[JsonProperty("label")]
		public string Label
		{
			get;
			set;
		}

		[JsonProperty("format", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Format
		{
			get;
			set;
		}

		[JsonIgnore]
		public TypeValue? Type
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

		[JsonProperty("width", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Width
		{
			get;
			set;
		}

		string INamed.Name => AttributeName;

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public ColumnDescriptor(string attributeName, string label, string format = null, TypeValue? type = null, int? width = null)
		{
			AttributeName = attributeName;
			Label = label;
			Format = format;
			Type = type;
			Width = width;
		}

		public ColumnDescriptor()
		{
		}

		public ColumnDescriptor(string attributeName, string label)
		{
			AttributeName = attributeName;
			Label = label;
		}
	}
}
