using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
	public class SetSymbolOptionsRequest : DebugRequest<SetSymbolOptionsArguments>
	{
		public const string RequestType = "setSymbolOptions";

		[JsonIgnore]
		public SymbolOptions SymbolOptions
		{
			get
			{
				return base.Args.SymbolOptions;
			}
			set
			{
				base.Args.SymbolOptions = value;
			}
		}

		public SetSymbolOptionsRequest()
			: base("setSymbolOptions")
		{
		}

		public SetSymbolOptionsRequest(SymbolOptions symbolOptions)
			: base("setSymbolOptions")
		{
			base.Args.SymbolOptions = symbolOptions;
		}
	}
}
