using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.DebugAdapter
{
    struct SequencePoint
    {
        public uint Address;
        public string Document;
        public (int line, int column) Start;
        public (int line, int column) End;

        public static SequencePoint Parse(JToken json)
        {
            (int, int) ParsePosition(string prefix)
            {
                return (json.Value<int>($"{prefix}-line"), json.Value<int>($"{prefix}-column"));
            }

            return new SequencePoint
            {
                Address = json.Value<uint>("address"),
                Document = json.Value<string>("document"),
                Start = ParsePosition("start"),
                End = ParsePosition("end")
            };
        }
    }

    internal class NeoDebugAdapter : DebugAdapterBase
    {
        Action<LogCategory, string> logger;

        public NeoDebugAdapter(Stream @in, Stream @out, Action<LogCategory, string> logger = null)
        {
            Action<LogCategory, string> nullLogger = (__, _) => { };
            this.logger = logger ?? nullLogger;

            InitializeProtocolClient(@in, @out);
        }

        public void Run()
        {
            Protocol.Run();
        }

        void Log(string message, LogCategory cat = LogCategory.DebugAdapterOutput)
        {
            logger(cat, message);
        }

        protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
        {
            this.Protocol.SendEvent(new InitializedEvent());


            return new InitializeResponse()
            {
                SupportsConfigurationDoneRequest = true,
            };
        }

        protected override ConfigurationDoneResponse HandleConfigurationDoneRequest(ConfigurationDoneArguments arguments)
        {
            return new ConfigurationDoneResponse();
        }

        string programFileName;
        SequencePoint[] sequencePoints;

        protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
        {
            programFileName = (string)arguments.ConfigurationProperties["program"];

            var debugJsonFileName = Path.ChangeExtension(programFileName, ".debug.json");
            var debugJsonText = File.ReadAllText(debugJsonFileName);
            sequencePoints = JArray.Parse(debugJsonText).Select(SequencePoint.Parse).ToArray();

            Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry) { ThreadId = 1 });
            return new LaunchResponse();
        }

        protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
        {
            return new DisconnectResponse();
        }

        protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
        {
            return new SetExceptionBreakpointsResponse();
        }

        protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments arguments)
        {
            return new ThreadsResponse()
            {
                Threads = new List<Thread>() { new Thread(1, "thread 1") },
            };
        }

        protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
        {
            var sp = sequencePoints[0];

            var stackFrame = new StackFrame()
            {
                Id = 0,
                Name = "frame 0",
                Source = new Source()
                {
                    Name = Path.GetFileName(sp.Document),
                    Path = sp.Document
                },
                Line = sp.Start.line,
                Column = sp.Start.column,
                EndLine = sp.End.line,
                EndColumn = sp.End.column,
            };
            return new StackTraceResponse(new List<StackFrame> { stackFrame });
        }

        protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
        {
            return new ScopesResponse();
        }

        protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
        {
            return new VariablesResponse();
        }

        protected override NextResponse HandleNextRequest(NextArguments arguments)
        {
            return new NextResponse();
        }

        protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
        {
            return new ContinueResponse();
        }

        protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
        {
            return new StepInResponse();
        }

        protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
        {
            return new StepOutResponse();
        }
    }
}
