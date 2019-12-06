using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ExceptionInfoResponse : ResponseBody
	{
		[JsonProperty("breakMode")]
		private EnumValue<ExceptionBreakMode> _breakMode = new EnumValue<ExceptionBreakMode>();

		[JsonProperty("exceptionId")]
		public string ExceptionId
		{
			get;
			set;
		}

		[JsonProperty("description", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public string Description
		{
			get;
			set;
		}

		[JsonIgnore]
		public ExceptionBreakMode BreakMode
		{
			get
			{
				return _breakMode.Value;
			}
			set
			{
				_breakMode.Value = value;
			}
		}

		[JsonProperty("details", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public ExceptionDetails Details
		{
			get;
			set;
		}

		[JsonProperty("code", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int? Code
		{
			get;
			set;
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public ExceptionInfoResponse(string exceptionId, ExceptionBreakMode breakMode, string description = null, ExceptionDetails details = null, int? code = null)
		{
			ExceptionId = exceptionId;
			Description = description;
			BreakMode = breakMode;
			Details = details;
			Code = code;
		}

		public ExceptionInfoResponse()
		{
		}

		public ExceptionInfoResponse(string exceptionId, ExceptionBreakMode breakMode)
		{
			ExceptionId = exceptionId;
			BreakMode = breakMode;
		}
	}
}
