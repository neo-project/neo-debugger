﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.VM;
using Helper = Neo.SmartContract.Helper;

namespace NeoDebug.Neo3
{
    class DisassemblyManager
    {
        public delegate bool TryGetScript(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script);
        public delegate bool TryGetDebugInfo(UInt160 scriptHash, [MaybeNullWhen(false)] out DebugInfo debugInfo);

        private static readonly ImmutableDictionary<uint, string> sysCallNames;

        static DisassemblyManager()
        {
            sysCallNames = ApplicationEngine.Services
                .ToImmutableDictionary(kvp => kvp.Value.Hash, kvp => kvp.Value.Name);
        }

        public readonly struct Disassembly
        {
            public readonly string Name;
            public readonly string Source;
            public readonly int SourceReference;
            public readonly ImmutableDictionary<int, int> AddressMap;
            public readonly ImmutableDictionary<int, int> LineMap;

            public Disassembly(string name, string source, int sourceReference, ImmutableDictionary<int, int> addressMap, ImmutableDictionary<int, int> lineMap)
            {
                this.Name = name;
                this.Source = source;
                this.SourceReference = sourceReference;
                this.AddressMap = addressMap;
                this.LineMap = lineMap;
            }
        }

        private readonly ConcurrentDictionary<int, Disassembly> disassemblies = new ConcurrentDictionary<int, Disassembly>();
        private readonly TryGetScript tryGetScript;
        private readonly TryGetDebugInfo tryGetDebugInfo;

        public DisassemblyManager(TryGetScript tryGetScript, TryGetDebugInfo tryGetDebugInfo)
        {
            this.tryGetScript = tryGetScript;
            this.tryGetDebugInfo = tryGetDebugInfo;
        }

        public Disassembly GetDisassembly(IExecutionContext context, DebugInfo? debugInfo)
            => GetDisassembly(context.ScriptIdentifier, context.Script, debugInfo);

        public Disassembly GetDisassembly(UInt160 scriptHash, Script script, DebugInfo? debugInfo)
            => disassemblies.GetOrAdd(scriptHash.GetHashCode(), sourceRef => ToDisassembly(sourceRef, scriptHash, script, debugInfo));

        public bool TryGetDisassembly(UInt160 scriptHash, out Disassembly disassembly)
        {
            if (tryGetScript(scriptHash, out var script))
            {
                var debugInfo = tryGetDebugInfo(scriptHash, out var _debugInfo) ? _debugInfo : null;
                disassembly = GetDisassembly(scriptHash, script, debugInfo);
                return true;
            }

            disassembly = default;
            return false;
        }

        public bool TryGetDisassembly(int sourceRef, out Disassembly disassembly)
            => disassemblies.TryGetValue(sourceRef, out disassembly);

        static Disassembly ToDisassembly(int sourceRef, UInt160 scriptHash, Script script, DebugInfo? debugInfo)
        {
            var padString = script.GetInstructionAddressPadding();
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

            var instructions = script.EnumerateInstructions().ToList();

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
                        var srcLine = doc.lines[sp.Start.line - 1].Substring(sp.Start.column - 1);
                        if (sp.Start.line == sp.End.line)
                        {
                            srcLine = srcLine.Substring(0, sp.End.column - sp.Start.column);
                        }

                        sourceBuilder.AppendLine($"# Code {doc.fileName} line {sp.Start.line}: \"{srcLine}\"");
                        line++;
                    }
                }

                AddSource(sourceBuilder, instructions[i].address, instructions[i].instruction, padString);
                addressMapBuilder.Add(instructions[i].address, line);
                lineMapBuilder.Add(line, instructions[i].address);
                line++;

                if (methodEnds.TryGetValue(instructions[i].address, out var methodEnd))
                {
                    sourceBuilder.Append($"\n# End Method {methodEnd.Namespace}.{methodEnd.Name}");
                    line++;
                }
            }

            return new Disassembly(
                scriptHash.ToString(),
                sourceBuilder.ToString(),
                sourceRef,
                addressMapBuilder.ToImmutable(),
                lineMapBuilder.ToImmutable());

            static void AddSource(StringBuilder sourceBuilder, int address, Instruction instruction, string padString)
            {
                sourceBuilder.Append($"{address.ToString(padString)} {instruction.OpCode}");
                if (!instruction.Operand.IsEmpty)
                {
                    sourceBuilder.Append($" {instruction.GetOperandString()}");
                }
                var comment = instruction.GetComment(address);
                if (comment.Length > 0)
                {
                    sourceBuilder.Append($" # {comment}");
                }
            }
        }
    }
}
