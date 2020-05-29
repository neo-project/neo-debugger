using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug;

namespace NeoDebug.Neo3
{
    class DebugSession : IDebugSession
    {
        public void Continue()
        {
            throw new NotImplementedException();
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            throw new NotImplementedException();
        }

        public SourceResponse GetSource(SourceArguments arguments)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Thread> GetThreads()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            throw new NotImplementedException();
        }

        public void SetDebugView(DebugView debugView)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void StepIn()
        {
            throw new NotImplementedException();
        }

        public void StepOut()
        {
            throw new NotImplementedException();
        }

        public void StepOver()
        {
            throw new NotImplementedException();
        }
    }
}
