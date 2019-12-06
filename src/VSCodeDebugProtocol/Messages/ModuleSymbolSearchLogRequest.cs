using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class ModuleSymbolSearchLogRequest : DebugRequestWithResponse<ModuleSymbolSearchLogArguments, ModuleSymbolSearchLogResponse>
	{
		public const string RequestType = "moduleSymbolSearchLog";

		[JsonIgnore]
		public object Id
		{
			get
			{
				return base.Args.Id;
			}
			set
			{
				base.Args.Id = value;
			}
		}

		public ModuleSymbolSearchLogRequest()
			: base("moduleSymbolSearchLog")
		{
		}

		public ModuleSymbolSearchLogRequest(object id)
			: base("moduleSymbolSearchLog")
		{
			base.Args.Id = id;
		}
	}
}
