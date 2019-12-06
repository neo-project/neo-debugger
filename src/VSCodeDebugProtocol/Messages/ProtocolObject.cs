using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public abstract class ProtocolObject
	{
		private Dictionary<string, JToken> _additionalProperties;

		[JsonExtensionData(ReadData = true, WriteData = true)]
		protected Dictionary<string, JToken> AdditionalProperties
		{
			get
			{
				if (_additionalProperties == null)
				{
					_additionalProperties = new Dictionary<string, JToken>();
				}
				return _additionalProperties;
			}
			set
			{
				_additionalProperties = value;
			}
		}
	}
}
