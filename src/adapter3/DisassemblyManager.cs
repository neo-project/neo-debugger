using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.VM;

namespace NeoDebug.Neo3
{
    class DisassemblyManager
    {
        public delegate bool TryGetScript(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script);
        public delegate bool TryGetDebugInfo(UInt160 scriptHash, [MaybeNullWhen(false)] out DebugInfo debugInfo);

        static readonly ImmutableDictionary<uint, string> sysCallNames;

        static DisassemblyManager()
        {
            sysCallNames = ApplicationEngine.Services
                .ToImmutableDictionary(kvp => kvp.Value.Hash, kvp => kvp.Value.Name);
        }

        public readonly record struct Disassembly
        {
            public readonly UInt160 ScriptHash { get; init; }
            public readonly string Source { get; init; }
            public readonly int SourceReference { get; init; }
            public readonly IReadOnlyDictionary<int, int> AddressMap { get; init; }
            public readonly IReadOnlyDictionary<int, int> LineMap { get; init; }
        }

        readonly ConcurrentDictionary<int, Disassembly> disassemblies = new ConcurrentDictionary<int, Disassembly>();
        readonly TryGetScript tryGetScript;
        readonly TryGetDebugInfo tryGetDebugInfo;

        public DisassemblyManager(TryGetScript tryGetScript, TryGetDebugInfo tryGetDebugInfo)
        {
            this.tryGetScript = tryGetScript;
            this.tryGetDebugInfo = tryGetDebugInfo;
        }

        public Disassembly GetOrAdd(IExecutionContext context, DebugInfo? debugInfo)
            => disassemblies.GetOrAdd(context.ScriptIdentifier.GetHashCode(), sourceRef => ToDisassembly(sourceRef, context, debugInfo));

        public bool TryGet(int sourceRef, out Disassembly disassembly)
            => disassemblies.TryGetValue(sourceRef, out disassembly);

        static Disassembly ToDisassembly(int sourceRef, IExecutionContext context, DebugInfo? debugInfo)
        {
            var padString = context.Script.GetInstructionAddressPadding();
            var sourceBuilder = new StringBuilder();
            var addressMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();
            var lineMapBuilder = ImmutableDictionary.CreateBuilder<int, int>();

            var documents = debugInfo?.Documents
                .Select(path => (fileName: Path.GetFileName(path), lines: File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>()))
                .ToImmutableList() ?? ImmutableList<(string, string[])>.Empty;
            var methodStarts = debugInfo?.Methods.ToImmutableDictionary(m => m.Range.Start)
                ?? ImmutableDictionary<int, DebugInfo.Method>.Empty;
            var methodEnds = debugInfo?.Methods.ToImmutableDictionary(m => m.Range.End)
                ?? ImmutableDictionary<int, DebugInfo.Method>.Empty;
            var sequencePoints = debugInfo?.Methods.SelectMany(m => m.SequencePoints).ToImmutableDictionary(s => s.Address)
                ?? ImmutableDictionary<int, DebugInfo.SequencePoint>.Empty;

            var instructions = context.Script.EnumerateInstructions().ToList();

            var line = 1;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (sourceBuilder.Length > 0) sourceBuilder.Append("\n");

                if (methodStarts.TryGetValue(instructions[i].address, out var methodStart))
                {
                    sourceBuilder.AppendLine($"# Start Method {methodStart.Namespace}.{methodStart.Name}");
                    line++;
                }

                if (sequencePoints.TryGetValue(instructions[i].address, out var sp)
                    && sp.Document < documents.Count)
                {
                    var doc = documents[sp.Document];
                    if (doc.lines.Length > sp.Start.line - 1)
                    {
                        var srcLine = doc.lines[sp.Start.line - 1];

                        if (sp.Start.column > 1) srcLine = srcLine.Substring(sp.Start.column - 1);
                        if (sp.Start.line == sp.End.line && sp.End.column > sp.Start.column)
                        {
                            srcLine = srcLine.Substring(0, sp.End.column - sp.Start.column);
                        }

                        sourceBuilder.AppendLine($"# Code {doc.fileName} line {sp.Start.line}: \"{srcLine.Trim()}\"");
                        line++;
                    }
                }

                AddSource(sourceBuilder, instructions[i].address, instructions[i].instruction, padString, (MethodToken[]?)context.Tokens);
                addressMapBuilder.Add(instructions[i].address, line);
                lineMapBuilder.Add(line, instructions[i].address);
                line++;

                if (methodEnds.TryGetValue(instructions[i].address, out var methodEnd))
                {
                    sourceBuilder.Append($"\n# End Method {methodEnd.Namespace}.{methodEnd.Name}");
                    line++;
                }
            }

            return new Disassembly
            {
                ScriptHash = context.ScriptIdentifier,
                Source = sourceBuilder.ToString(),
                SourceReference = sourceRef,
                AddressMap = addressMapBuilder.ToImmutable(),
                LineMap = lineMapBuilder.ToImmutable()
            };

            static void AddSource(StringBuilder sourceBuilder, int address, Instruction instruction, string padString, MethodToken[]? tokens)
            {
                sourceBuilder.Append($"{address.ToString(padString)} {instruction.OpCode}");
                if (!instruction.Operand.IsEmpty)
                {
                    sourceBuilder.Append($" {instruction.GetOperandString()}");
                }
                var comment = instruction.GetComment(address, tokens);
                if (comment.Length > 0)
                {
                    sourceBuilder.Append($" # {comment}");
                }
            }
        }
    }
}
