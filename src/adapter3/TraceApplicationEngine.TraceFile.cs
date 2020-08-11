﻿using System;
using System.Collections.Generic;
using Neo;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using MessagePack;
using MessagePack.Resolvers;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Ledger;
using System.Linq;

namespace NeoDebug.Neo3
{
    internal partial class TraceApplicationEngine
    {
        private sealed class TraceFile : IDisposable
        {
            private bool disposedValue;
            private readonly MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard
                .WithResolver(TraceDebugResolver.Instance);
            private readonly Stream traceFileStream;
            private readonly Stack<ITraceDebugRecord> previousRecords = new Stack<ITraceDebugRecord>();
            private readonly Stack<ITraceDebugRecord> nextRecords = new Stack<ITraceDebugRecord>();
            private readonly IDictionary<UInt160, Script> contracts;

            public TraceFile(string traceFilePath, IDictionary<UInt160, Script> contracts)
            {
                this.traceFileStream = File.OpenRead(traceFilePath);
                this.contracts = contracts;
            }

            public void Dispose()
            {
                if (!disposedValue)
                {
                    traceFileStream.Dispose();
                    disposedValue = true;
                }
            }

            public bool TryGetPrev([MaybeNullWhen(false)] out ITraceDebugRecord record)
            {
                if (disposedValue) throw new ObjectDisposedException(nameof(TraceFile));

                if (previousRecords.TryPop(out record))
                {
                    nextRecords.Push(record);
                    return true;
                }

                return false;
            }

            public bool TryGetNext([MaybeNullWhen(false)] out ITraceDebugRecord record)
            {
                if (disposedValue) throw new ObjectDisposedException(nameof(TraceFile));

                if (nextRecords.TryPop(out record))
                {
                    previousRecords.Push(record);
                    return true;
                }

                while (traceFileStream.Position < traceFileStream.Length)
                {
                    record = MessagePackSerializer.Deserialize<ITraceDebugRecord>(traceFileStream, options);
                    if (record is ScriptRecord script)
                    {
                        contracts.TryAdd(script.ScriptHash, script.Script);
                    }
                    else
                    {
                        previousRecords.Push(record);
                        return true;
                    }
                }

                return false;
            }

            public IEnumerable<(ReadOnlyMemory<byte> key, StorageItem value)> FindStorage(UInt160 scriptHash)
            {
                foreach (var rec in previousRecords)
                {
                    if (rec is StorageRecord storage
                        && storage.ScriptHash.Equals(scriptHash))
                    {
                        return storage.Storages.Select(t => ((ReadOnlyMemory<byte>)t.Key, t.Value));
                    }
                }

                return Enumerable.Empty<(ReadOnlyMemory<byte>, StorageItem)>();
            }
        }
    }
}
