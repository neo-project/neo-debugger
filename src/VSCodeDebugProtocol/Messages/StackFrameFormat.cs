using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class StackFrameFormat : ValueFormat
	{
		[JsonProperty("parameters", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? Parameters
		{
			get;
			set;
		}

		[JsonProperty("parameterTypes", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? ParameterTypes
		{
			get;
			set;
		}

		[JsonProperty("parameterNames", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? ParameterNames
		{
			get;
			set;
		}

		[JsonProperty("parameterValues", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? ParameterValues
		{
			get;
			set;
		}

		[JsonProperty("line", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? Line
		{
			get;
			set;
		}

		[JsonProperty("module", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? Module
		{
			get;
			set;
		}

		[JsonProperty("includeAll", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? IncludeAll
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public StackFrameFormat(bool? parameters = null, bool? parameterTypes = null, bool? parameterNames = null, bool? parameterValues = null, bool? line = null, bool? module = null, bool? includeAll = null)
		{
			Parameters = parameters;
			ParameterTypes = parameterTypes;
			ParameterNames = parameterNames;
			ParameterValues = parameterValues;
			Line = line;
			Module = module;
			IncludeAll = includeAll;
		}

		public StackFrameFormat()
		{
		}
	}
}
