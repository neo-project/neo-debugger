using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using NeoDebug.VariableContainers;
using NeoFx.Models;
using NeoFx.Storage;
using System.Collections.Generic;



namespace NeoDebug.ModelAdapters
{
    class BlockHeaderAdapter : AdapterBase, IVariableProvider, IVariableContainer
    {
        public readonly BlockHeader Item;

        public BlockHeaderAdapter(in BlockHeader value)
        {
            Item = value;
        }

        public static BlockHeaderAdapter Create(in BlockHeader value)
        {
            return new BlockHeaderAdapter(value);
        }

        public bool GetHash(ExecutionEngine engine, IBlockchainStorage? blockchain)
        {
            // TODO: should I be calculating the hash here, or is it OK to simply retrieve the hash
            //       by block index?
            if (blockchain != null
                && blockchain.TryGetBlockHash(Item.Index, out var hash)
                && hash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetVersion(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Item.Version);
            return true;
        }

        public bool GetPrevHash(ExecutionEngine engine)
        {
            if (Item.PreviousHash.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetMerkleRoot(ExecutionEngine engine)
        {
            if (Item.MerkleRoot.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public bool GetTimestamp(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((uint)Item.Timestamp.ToUnixTimeSeconds());
            return true;
        }

        public bool GetIndex(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.Index);
            return true;
        }

        public bool GetConsensusData(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Item.ConsensusData);
            return true;
        }

        public bool GetNextConsensus(ExecutionEngine engine)
        {
            if (Item.NextConsensus.TryToArray(out var array))
            {
                engine.CurrentContext.EvaluationStack.Push(array);
                return true;
            }

            return false;
        }

        public Variable GetVariable(IVariableContainerSession session, string name)
        {
            return new Variable()
            {
                Name = name,
                Type = "BlockHeader",
                Value = string.Empty,
                VariablesReference = session.AddVariableContainer(new AdapterVariableContainer(this)),
                NamedVariables = 2,
            };
        }

        public IEnumerable<Variable> GetVariables()
        {
            yield return new Variable()
            {
                Name = "Version",
                Value = Item.Version.ToString(),
            };

            yield return new Variable()
            {
                Name = "Index",
                Value = Item.Index.ToString(),
            };

            yield return new Variable()
            {
                Name = "PreviousHash",
                Value = Item.PreviousHash.ToString(),
            };

            yield return new Variable()
            {
                Name = "MerkleRoot",
                Value = Item.MerkleRoot.ToString(),
            };

            yield return new Variable()
            {
                Name = "Timestamp",
                Value = Item.Timestamp.ToString(),
            };

            yield return new Variable()
            {
                Name = "NextConsensus",
                Value = Item.NextConsensus.ToString(),
            };

            yield return new Variable()
            {
                Name = "ConsensusData",
                Value = Item.ConsensusData.ToString(),
            };
        }
    }
}
