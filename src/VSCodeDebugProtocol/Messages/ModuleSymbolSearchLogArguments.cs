using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ModuleSymbolSearchLogArguments : DebugRequestArguments
	{
		[JsonProperty("id")]
		public object Id
		{
			get;
			set;
		}
	}
}
