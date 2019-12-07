using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class RunInTerminalRequest : DebugClientRequestWithResponse<RunInTerminalArguments, RunInTerminalResponse>
    {
        public const string RequestType = "runInTerminal";

        [JsonIgnore]
        public RunInTerminalArguments.KindValue? Kind
        {
            get
            {
                return base.Args.Kind;
            }
            set
            {
                base.Args.Kind = value;
            }
        }

        [JsonIgnore]
        public string Title
        {
            get
            {
                return base.Args.Title;
            }
            set
            {
                base.Args.Title = value;
            }
        }

        [JsonIgnore]
        public string Cwd
        {
            get
            {
                return base.Args.Cwd;
            }
            set
            {
                base.Args.Cwd = value;
            }
        }

        [JsonIgnore]
        public List<string> Arguments
        {
            get
            {
                return base.Args.Args;
            }
            set
            {
                base.Args.Args = value;
            }
        }

        [JsonIgnore]
        public Dictionary<string, object> Env
        {
            get
            {
                return base.Args.Env;
            }
            set
            {
                base.Args.Env = value;
            }
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public RunInTerminalRequest(string cwd, List<string> args, RunInTerminalArguments.KindValue? kind = null, string title = null, Dictionary<string, object> env = null)
            : base("runInTerminal")
        {
            base.Args.Kind = kind;
            base.Args.Title = title;
            base.Args.Cwd = cwd;
            base.Args.Args = args;
            base.Args.Env = env;
        }

        public RunInTerminalRequest()
            : base("runInTerminal")
        {
            base.Args.Args = new List<string>();
            base.Args.Env = new Dictionary<string, object>();
        }

        public RunInTerminalRequest(string cwd, List<string> args)
            : base("runInTerminal")
        {
            base.Args.Cwd = cwd;
            base.Args.Args = args;
        }
    }
}
