using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class DataBreakpointInfoResponse : ResponseBody
	{
		[JsonProperty("dataId")]
		public object DataId
		{
			get;
			set;
		}

		[JsonProperty("description")]
		public string Description
		{
			get;
			set;
		}

		[JsonProperty("accessTypes", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public List<DataBreakpointAccessType> AccessTypes
		{
			get;
			set;
		}

		[JsonProperty("canPersist", DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool? CanPersist
		{
			get;
			set;
		}

		public DataBreakpointInfoResponse()
		{
			AccessTypes = new List<DataBreakpointAccessType>();
		}

		public DataBreakpointInfoResponse(object dataId, string description)
		{
			DataId = dataId;
			Description = description;
		}
	}
}
