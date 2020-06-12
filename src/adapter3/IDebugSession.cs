using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.Collections.Generic;

namespace NeoDebug.Neo3
{
    public interface IDebugSession
    {
        void Continue();
        EvaluateResponse Evaluate(EvaluateArguments args);
        IEnumerable<Scope> GetScopes(ScopesArguments args);
        SourceResponse GetSource(SourceArguments arguments);
        IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args);
        IEnumerable<Thread> GetThreads();
        IEnumerable<Variable> GetVariables(VariablesArguments args);
        IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints);
        void SetDebugView(DebugView debugView);
        void Start();
        void StepIn();
        void StepOut();
        void StepOver();
    }
}
