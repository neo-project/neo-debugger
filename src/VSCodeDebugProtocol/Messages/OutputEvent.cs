using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class OutputEvent : DebugEvent
	{
		public enum CategoryValue
		{
			[EnumMember(Value = "console")]
			Console = 0,
			[EnumMember(Value = "stdout")]
			Stdout = 1,
			[EnumMember(Value = "stderr")]
			Stderr = 2,
			[EnumMember(Value = "telemetry")]
			Telemetry = 3,
			[DefaultEnumValue]
			Unknown = int.MaxValue
		}

		public const string EventType = "output";

		[JsonProperty("category", DefaultValueHandling = DefaultValueHandling.Ignore)]
		private NullableEnumValue<CategoryValue> _category = new NullableEnumValue<CategoryValue>();

		[JsonIgnore]
		public CategoryValue? Category
		{
			get
			{
				return _category.Value;
			}
			set
			{
				_category.Value = value;
			}
		}

		[JsonProperty("output")]
		public string Output
		{
			get;
			set;
		}

		[JsonProperty("variablesReference", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? VariablesReference
		{
			get;
			set;
		}

		[JsonProperty("source", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public Source Source
		{
			get;
			set;
		}

		[JsonProperty("line", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Line
		{
			get;
			set;
		}

		[JsonProperty("column", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Column
		{
			get;
			set;
		}

		[JsonProperty("data", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public object Data
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public OutputEvent(string output, CategoryValue? category = null, int? variablesReference = null, Source source = null, int? line = null, int? column = null, object data = null)
			: base("output")
		{
			Category = category;
			Output = output;
			VariablesReference = variablesReference;
			Source = source;
			Line = line;
			Column = column;
			Data = data;
		}

		public OutputEvent()
			: base("output")
		{
		}

		public OutputEvent(string output)
			: base("output")
		{
			Output = output;
		}
	}
}
