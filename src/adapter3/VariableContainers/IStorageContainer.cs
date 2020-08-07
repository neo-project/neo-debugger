using System;

namespace NeoDebug.Neo3
{
    internal interface IStorageContainer : IVariableContainer
    {
        Neo.VM.Types.StackItem? Evaluate(ReadOnlyMemory<char> expression);
    }
}
