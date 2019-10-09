using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter.ModelAdapters
{
    class BlockHeaderAdapter : AdapterBase, IVariableProvider, IVariableContainer
    {
        public readonly BlockHeader Value;

        public BlockHeaderAdapter(in BlockHeader value)
        {
            Value = value;
        }

        public static BlockHeaderAdapter Create(in BlockHeader value)
        {
            return new BlockHeaderAdapter(value);
        }

        public bool GetHash(ExecutionEngine engine, IBlockchainStorage blockchain)
        {
            // TODO: should I be calculating the hash here, or is it OK to simply retrieve the hash
            //       by block index?
            if (blockchain.TryGetBlockHash(Value.Index, out var hash)
                && hash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetVersion(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Value.Version);
            return true;
        }

        public bool GetPrevHash(ExecutionEngine engine)
        {
            if (Value.PreviousHash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetMerkleRoot(ExecutionEngine engine)
        {
            if (Value.MerkleRoot.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetTimestamp(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((uint)Value.Timestamp.ToUnixTimeSeconds());
            return true;
        }

        public bool GetIndex(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Value.Index);
            return true;
        }

        public bool GetConsensusData(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Value.ConsensusData);
            return true;
        }

        public bool GetNextConsensus(ExecutionEngine engine)
        {
            if (Value.NextConsensus.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public Variable GetVariable(IVariableContainerSession session)
        {
            return new Variable()
            {
                Type = "BlockHeader",
                VariablesReference = session.AddVariableContainer(new AdapterVariableContainer(this)),
                NamedVariables = 2,
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            yield return new Variable()
            {
                Name = "Version",
                Value = Value.Version.ToString(),
            };

            yield return new Variable()
            {
                Name = "Index",
                Value = Value.Index.ToString(),
            };

            yield return new Variable()
            {
                Name = "PreviousHash",
                Value = Value.PreviousHash.ToString(),
            };

            yield return new Variable()
            {
                Name = "MerkleRoot",
                Value = Value.MerkleRoot.ToString(),
            };

            yield return new Variable()
            {
                Name = "Timestamp",
                Value = Value.Timestamp.ToString(),
            };

            yield return new Variable()
            {
                Name = "NextConsensus",
                Value = Value.NextConsensus.ToString(),
            };

            yield return new Variable()
            {
                Name = "ConsensusData",
                Value = Value.ConsensusData.ToString(),
            };
        }
    }
}
