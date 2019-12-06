using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class InitializeRequest : DebugRequestWithResponse<InitializeArguments, InitializeResponse>
	{
		public const string RequestType = "initialize";

		[JsonIgnore]
		public string ClientID
		{
			get
			{
				return base.Args.ClientID;
			}
			set
			{
				base.Args.ClientID = value;
			}
		}

		[JsonIgnore]
		public string ClientName
		{
			get
			{
				return base.Args.ClientName;
			}
			set
			{
				base.Args.ClientName = value;
			}
		}

		[JsonIgnore]
		public string AdapterID
		{
			get
			{
				return base.Args.AdapterID;
			}
			set
			{
				base.Args.AdapterID = value;
			}
		}

		[JsonIgnore]
		public string Locale
		{
			get
			{
				return base.Args.Locale;
			}
			set
			{
				base.Args.Locale = value;
			}
		}

		[JsonIgnore]
		public bool? LinesStartAt1
		{
			get
			{
				return base.Args.LinesStartAt1;
			}
			set
			{
				base.Args.LinesStartAt1 = value;
			}
		}

		[JsonIgnore]
		public bool? ColumnsStartAt1
		{
			get
			{
				return base.Args.ColumnsStartAt1;
			}
			set
			{
				base.Args.ColumnsStartAt1 = value;
			}
		}

		[JsonIgnore]
		public InitializeArguments.PathFormatValue? PathFormat
		{
			get
			{
				return base.Args.PathFormat;
			}
			set
			{
				base.Args.PathFormat = value;
			}
		}

		[JsonIgnore]
		public bool? SupportsVariableType
		{
			get
			{
				return base.Args.SupportsVariableType;
			}
			set
			{
				base.Args.SupportsVariableType = value;
			}
		}

		[JsonIgnore]
		public bool? SupportsVariablePaging
		{
			get
			{
				return base.Args.SupportsVariablePaging;
			}
			set
			{
				base.Args.SupportsVariablePaging = value;
			}
		}

		[JsonIgnore]
		public bool? SupportsRunInTerminalRequest
		{
			get
			{
				return base.Args.SupportsRunInTerminalRequest;
			}
			set
			{
				base.Args.SupportsRunInTerminalRequest = value;
			}
		}

		[JsonIgnore]
		public bool? SupportsMemoryReferences
		{
			get
			{
				return base.Args.SupportsMemoryReferences;
			}
			set
			{
				base.Args.SupportsMemoryReferences = value;
			}
		}

		[JsonIgnore]
		public bool? SupportsHandshakeRequest
		{
			get
			{
				return base.Args.SupportsHandshakeRequest;
			}
			set
			{
				base.Args.SupportsHandshakeRequest = value;
			}
		}

		[JsonIgnore]
		public Dictionary<string, JToken> AdditionalProperties
		{
			get
			{
				return base.Args.AdditionalProperties;
			}
			set
			{
				base.Args.AdditionalProperties = value;
			}
		}

		[Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
		public InitializeRequest(string adapterID, string clientID = null, string locale = null, bool? linesStartAt1 = null, bool? columnsStartAt1 = null, InitializeArguments.PathFormatValue? pathFormat = null, bool? supportsVariableType = null, bool? supportsVariablePaging = null, bool? supportsRunInTerminalRequest = null, bool? supportsHandshakeRequest = null, Dictionary<string, JToken> additionalProperties = null)
			: base("initialize")
		{
			base.Args.ClientID = clientID;
			base.Args.AdapterID = adapterID;
			base.Args.Locale = locale;
			base.Args.LinesStartAt1 = linesStartAt1;
			base.Args.ColumnsStartAt1 = columnsStartAt1;
			base.Args.PathFormat = pathFormat;
			base.Args.SupportsVariableType = supportsVariableType;
			base.Args.SupportsVariablePaging = supportsVariablePaging;
			base.Args.SupportsRunInTerminalRequest = supportsRunInTerminalRequest;
			base.Args.SupportsHandshakeRequest = supportsHandshakeRequest;
			base.Args.AdditionalProperties = additionalProperties;
		}

		public InitializeRequest()
			: base("initialize")
		{
		}

		public InitializeRequest(string adapterID)
			: base("initialize")
		{
			base.Args.AdapterID = adapterID;
		}
	}
}
