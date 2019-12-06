namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class LoadedSourcesRequest : DebugRequestWithResponse<LoadedSourcesArguments, LoadedSourcesResponse>
	{
		public const string RequestType = "loadedSources";

		public LoadedSourcesRequest()
			: base("loadedSources")
		{
		}
	}
}
