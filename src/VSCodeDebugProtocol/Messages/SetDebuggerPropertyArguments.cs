using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SetDebuggerPropertyArguments : DebugRequestArguments
	{
		[JsonExtensionData(ReadData = true, WriteData = true)]
		public Dictionary<string, JToken> DebuggerProperties
		{
			get
			{
				return base.AdditionalProperties;
			}
			set
			{
				base.AdditionalProperties = value;
			}
		}
	}
}
