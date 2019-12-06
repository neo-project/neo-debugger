using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class VSSourceLinkInfo : DebugType
	{
		[JsonProperty("url")]
		public string Url
		{
			get;
			set;
		}

		[JsonProperty("relativeFilePath")]
		public string RelativeFilePath
		{
			get;
			set;
		}

		public VSSourceLinkInfo()
		{
		}

		public VSSourceLinkInfo(string url, string relativeFilePath)
		{
			Url = url;
			RelativeFilePath = relativeFilePath;
		}
	}
}
