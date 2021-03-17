using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.Disposables;
using OneOf;
using Script = Neo.VM.Script;
using StackItem = Neo.VM.Types.StackItem;
namespace NeoDebug.Neo3
{
    using ConfigProps = IReadOnlyDictionary<string, JToken>;
    using Invocation = OneOf<LaunchConfigParser.InvokeFileInvocation, LaunchConfigParser.OracleResponseInvocation, LaunchConfigParser.LaunchInvocation, LaunchConfigParser.ContractDeployInvocation>;
    // using Storages = IEnumerable<(byte[] key, StorageItem item)>;

    partial class LaunchConfigParser
    {
        // readonly Dictionary<string, JToken> config;
        // readonly Dictionary<string, UInt160> contracts = new Dictionary<string, UInt160>();
        // readonly ContractParameterParser paramParser;

        public static async Task<IDebugSession> CreateDebugSessionAsync(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            var sourceFileMap = ImmutableDictionary<string, string>.Empty;
            if (launchArguments.ConfigurationProperties.TryGetValue("sourceFileMap", out var jsonSourceFileMap) && jsonSourceFileMap.Type == JTokenType.Object)
            {
                sourceFileMap = ((IEnumerable<KeyValuePair<string, JToken?>>)jsonSourceFileMap).ToImmutableDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Value<string>() ?? string.Empty);
            }

            var returnTypes = ImmutableList<string>.Empty;
            if (launchArguments.ConfigurationProperties.TryGetValue("return-types", out var jsonReturnTypes))
            {
                var builder = ImmutableList.CreateBuilder<string>();
                foreach (var returnType in jsonReturnTypes)
                {
                    builder.Add(VariableManager.CastOperations[returnType.Value<string>()]);
                }
                returnTypes = builder.ToImmutable();
            }

            var debugInfoList = await LoadDebugInfosAsync(launchArguments.ConfigurationProperties, sourceFileMap).ToListAsync().ConfigureAwait(false);
            var engine = await CreateEngineAsync(launchArguments.ConfigurationProperties).ConfigureAwait(false);
            return new DebugSession(engine, debugInfoList, returnTypes, sendEvent, defaultDebugView);
        }

        static string ParseProgram(ConfigProps config) => config["program"].Value<string>() ?? throw new JsonException("missing program property");

        static async Task<IApplicationEngine> CreateEngineAsync(ConfigProps config)
        {
            if (!config.TryGetValue("invocation", out var jsonInvocation))
            {
                throw new JsonException("missing invocation property");
            }

            if (jsonInvocation.Type == JTokenType.Object && jsonInvocation["trace-file"] != null)
            {
                var traceFile = jsonInvocation.Value<string>("trace-file") ?? throw new JsonException("invalid trace-file property");
                var program = ParseProgram(config);
                var launchContract = LoadNefFile(program);
                var contracts = new List<NefFile> { launchContract };

                // TODO: load other contracts?

                return new TraceApplicationEngine(traceFile, contracts);
            }

            return await CreateDebugEngineAsync(config, jsonInvocation).ConfigureAwait(false);
        }

        static async Task<IApplicationEngine> CreateDebugEngineAsync(ConfigProps config, JToken jsonInvocation)
        {
            var program = ParseProgram(config);
            var launchNefFile = LoadNefFile(program);
            var launchManifest = await LoadContractManifest(program).ConfigureAwait(false);

            ExpressChain? chain = null;
            if (config.TryGetValue("neo-express", out var neoExpressPath))
            {
                var fs = new System.IO.Abstractions.FileSystem();
                chain = fs.LoadChain(neoExpressPath.Value<string>());
            }

            Invocation invocation = InvokeFileInvocation.TryFromJson(jsonInvocation, out var invokeFileInvocation)
                ? invokeFileInvocation
                : OracleResponseInvocation.TryFromJson(jsonInvocation, out var oracleInvocation)
                    ? oracleInvocation
                    : LaunchInvocation.TryFromJson(jsonInvocation, out var launchInvocation)
                        ? launchInvocation
                        : ContractDeployInvocation.TryFromJson(jsonInvocation, out var deployInvocation)
                            ? deployInvocation
                            : throw new JsonException("invalid invocation property");

            var (store, settings) = LoadBlockchainStorage(config, chain?.Magic, chain?.AddressVersion);
            EnsureLedgerInitialized(store, settings);

            var (trigger, witnessChecker) = ParseRuntime(config, settings.AddressVersion);
            if (trigger != TriggerType.Application)
            {
                throw new Exception($"Trigger Type {trigger} not supported");
            }

            var signers = ParseSigners(config, settings.AddressVersion).ToArray();

            Script invokeScript;
            if (invocation.IsT3) // T3 == ContractDeploymentInvocation
            {
                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(NativeContract.ContractManagement.Hash, "deploy", launchNefFile.ToArray(), launchManifest.ToJson().ToString());
                invokeScript = builder.ToArray();
            }
            else
            {
                var (lauchContractId, launchContractHash) = EnsureContractDeployed(store, launchNefFile, launchManifest, signers, settings);
                var paramParser = CreateContractParameterParser(settings.AddressVersion, store, chain);
                invokeScript = await CreateInvokeScriptAsync(invocation, program, launchContractHash, paramParser);
            }

            // TODO: load other contracts
            //          Not sure supporting other contracts is a good idea anymore. Since there's no way to calcualte the 
            //          contract id hash prior to deployment in Neo 3, I'm thinking the better approach would be to simply
            //          deploy whatever contracts you want and take a snapshot rather than deploying multiple contracts 
            //          during launch configuration.

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = invokeScript,
                Signers = signers,
                ValidUntilBlock = Transaction.MaxValidUntilBlockIncrement,
                Attributes = Array.Empty<TransactionAttribute>(),
                // Attributes = GetTransactionAttributes(invocation, store, launchContractHash),
                Witnesses = Array.Empty<Witness>()
            };

            var block = CreateDummyBlock(store, tx);

            var engine = new DebugApplicationEngine(tx, new SnapshotCache(store), block, settings, witnessChecker);
            engine.LoadScript(invokeScript);
            return engine;
        }

        static NefFile LoadNefFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, false);
            return reader.ReadSerializable<NefFile>();
        }

        static async Task<ContractManifest> LoadContractManifest(string contractPath)
        {
            var bytesManifest = await File.ReadAllBytesAsync(
                Path.ChangeExtension(contractPath, ".manifest.json")).ConfigureAwait(false);
            return ContractManifest.Parse(bytesManifest);
        }

        static (IStore store, ProtocolSettings settings) LoadBlockchainStorage(ConfigProps config, uint? magic = null, byte? addressVersion = null)
        {
            if (config.TryGetValue("checkpoint", out var checkpoint))
            {
                string checkpointTempPath;
                do
                {
                    checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                }
                while (Directory.Exists(checkpointTempPath));

                var cleanup = AnonymousDisposable.Create(() =>
                {
                    if (Directory.Exists(checkpointTempPath))
                    {
                        Directory.Delete(checkpointTempPath);
                    }
                });

                var metadata = RocksDbStore.RestoreCheckpoint(checkpoint.Value<string>(), checkpointTempPath);
                if (magic.HasValue && magic.Value != metadata.magic)
                    throw new Exception($"checkpoint magic ({metadata.magic}) doesn't match ({magic.Value})");
                if (addressVersion.HasValue && addressVersion.Value != metadata.addressVersion)
                    throw new Exception($"checkpoint address version ({metadata.addressVersion}) doesn't match ({addressVersion.Value})");

                var store = new CheckpointStore(RocksDbStore.OpenReadOnly(checkpointTempPath), cleanup);
                var settings = ProtocolSettings.Default with
                {
                    Magic = metadata.magic,
                    AddressVersion = metadata.addressVersion
                };

                return (store, settings);
            }
            else
            {
                var settings = ProtocolSettings.Default with
                {
                    Magic = magic.HasValue ? magic.Value : ProtocolSettings.Default.Magic,
                    AddressVersion = addressVersion.HasValue ? addressVersion.Value : ProtocolSettings.Default.AddressVersion
                };
                return (new MemoryStore(), settings);
            }
        }

        static ContractParameterParser CreateContractParameterParser(byte addressVersion, IStore store, ExpressChain? chain)
        {
            var deployedContracts = ImmutableDictionary<string, UInt160>.Empty;
            using (var snapshot = new SnapshotCache(store))
            {
                deployedContracts = NativeContract.ContractManagement.ListContracts(snapshot)
                    .ToImmutableDictionary(
                        contract => contract.Manifest.Name,
                        contract => contract.Hash);
            }

            ContractParameterParser.TryGetUInt160? tryGetAccount = chain == null 
                ? null
                : (string name, out UInt160 scriptHash) =>
                {
                    if (chain.TryGetDefaultAccount(name, out var account))
                    {
                        scriptHash = account.ScriptHash.ToScriptHash(addressVersion);
                        return true;
                    }

                    scriptHash = null!;
                    return false;
                };

            return new ContractParameterParser(addressVersion, tryGetAccount, deployedContracts.TryGetValue);
        }


        // LaunchConfigParser(LaunchArguments launchArguments)
        // {
        //     config = launchArguments.ConfigurationProperties;

        //     var addressVersion = config.TryGetValue("address-version", out var addressVersionJson)
        //         ? addressVersionJson.Value<byte>()
        //         : ProtocolSettings.Default.AddressVersion;

        //     if (config.TryGetValue("neo-express", out var neoExpressPath))
        //     {
        //         var fs = new System.IO.Abstractions.FileSystem();
        //         chain = fs.LoadChain(neoExpressPath.Value<string>());
        //     }

        //     if (chain != null && chain.AddressVersion != addressVersion)
        //     {
        //         throw new Exception($"AddressVersion specified in {neoExpressPath} doesn't match value specified in launch.json");
        //     }

        //     ContractParameterParser.TryGetUInt160? tryGetAccount = chain == null ? null 
        //         : (string name, out UInt160 scriptHash) =>
        //         {
        //             if (chain.TryGetDefaultAccount(name, out var account))
        //             {
        //                 scriptHash = account.ScriptHash.ToScriptHash(addressVersion);
        //                 return true;
        //             }

        //             scriptHash = null!;
        //             return false;
        //         };

        //     paramParser = new ContractParameterParser(addressVersion, tryGetAccount, contracts.TryGetValue);
        // }





        //     var launchContractPath = config["program"].Value<string>() ?? throw new Exception("missing program config property");
        //     var launchContract = LoadContract(launchContractPath);
        //     var launchContractManifest = await LoadManifestAsync(launchContractPath);

        //     var store = CreateBlockchainStorage(config, protocolSettings);
        //     

        //     // ParseSigners accesses ProtocolSettings.Default so it needs to be after CreateBlockchainStorage
        //     var signers = ParseSigners(config, protocolSettings.AddressVersion).ToArray();
        //     var (lauchContractId, launchContractHash) = AddContract(store, protocolSettings, launchContract, launchContractManifest, signers);

        //     // TODO: load other contracts
        //     //          Not sure supporting other contracts is a good idea anymore. Since there's no way to calcualte the 
        //     //          contract id hash prior to deployment in Neo 3, I'm thinking the better approach would be to simply
        //     //          deploy whatever contracts you want and take a snapshot rather than deploying multiple contracts 
        //     //          during launch configuration.

        //     Block dummyBlock;
        //     {
        //         // cache the deployed contracts for use by parameter parser
        //         using var snapshot = new SnapshotCache(store);
        //         foreach (var contractState in NativeContract.ContractManagement.ListContracts(snapshot))
        //         {
        //             contracts.TryAdd(contractState.Manifest.Name, contractState.Hash);
        //         }

        //         dummyBlock = CreateDummyBlock(snapshot, protocolSettings);
        //     }

        //     UpdateContractStorage(store, lauchContractId, ParseStorage());

        //     // TODO: load other contract storage (unless we cut this feature - see comment above)

        //     var invokeScript = await CreateInvokeScriptAsync(invocation, launchContractHash).ConfigureAwait(false);

        //     var tx = new Transaction
        //     {
        //         Version = 0,
        //         Nonce = (uint)new Random().Next(),
        //         Script = invokeScript,
        //         Signers = signers,
        //         ValidUntilBlock = Transaction.MaxValidUntilBlockIncrement,
        //         Attributes = GetTransactionAttributes(invocation, store, launchContractHash),
        //         Witnesses = Array.Empty<Witness>()
        //     };

        //     var engine = new DebugApplicationEngine(tx, new SnapshotCache(store), dummyBlock, protocolSettings, witnessChecker);
        //     engine.LoadScript(invokeScript);
        //     return engine;

        static (int id, UInt160 scriptHash) EnsureContractDeployed(IStore store, NefFile nefFile, ContractManifest manifest, Signer[] signers, ProtocolSettings settings)
        {
            const byte Prefix_Contract = 8;

            using (var snapshotView = new SnapshotCache(store))
            {
                // check to see if there's a contract with a name that matches the name in the manifest
                foreach (var contract in NativeContract.ContractManagement.ListContracts(snapshotView))
                {
                    // do not update native contracts, even if names match
                    if (contract.Id <= 0) continue;

                    if (string.Equals(contract.Manifest.Name, manifest.Name))
                    {
                        // if the deployed script doesn't match the script parameter, overwrite the deployed script
                        if (nefFile.Script.ToScriptHash() != contract.Script.ToScriptHash())
                        {
                            using var snapshot = new SnapshotCache(store.GetSnapshot());
                            Update(snapshot, contract.Hash, nefFile, manifest, settings);
                            snapshot.Commit();
                        }

                        return (contract.Id, contract.Hash);
                    }
                }
            }

            // if no existing contract with matching manifest.Name is found, deploy the contract
            using (var snapshot = new SnapshotCache(store.GetSnapshot()))
            {
                var sender = signers.Length == 0 ? UInt160.Zero : signers[0].Account;
                var contractState = Deploy(snapshot, sender, nefFile, manifest, settings);
                snapshot.Commit();

                return (contractState.Id, contractState.Hash);
            }

            // following logic lifted from ContractManagement.Deploy
            static ContractState Deploy(DataCache snapshot, UInt160 sender, NefFile nefFile, ContractManifest manifest, ProtocolSettings settings)
            {
                Check(nefFile.Script, manifest.Abi);

                var hash = Neo.SmartContract.Helper.GetContractHash(sender, nefFile.CheckSum, manifest.Name);
                var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(hash);

                if (snapshot.Contains(key)) throw new InvalidOperationException($"Contract Already Exists: {hash}");
                if (!manifest.IsValid(hash)) throw new InvalidOperationException($"Invalid Manifest Hash: {hash}");

                var contract = new ContractState
                {
                    Id = GetNextAvailableId(snapshot),
                    UpdateCounter = 0,
                    Nef = nefFile,
                    Hash = hash,
                    Manifest = manifest
                };

                snapshot.Add(key, new StorageItem(contract));
                OnDeploy(contract, snapshot, settings, false);
                return contract;
            }

            // following logic lifted from ContractManagement.Update
            static void Update(DataCache snapshot, UInt160 contractHash, NefFile nefFile, ContractManifest manifest, ProtocolSettings settings)
            {
                Check(nefFile.Script, manifest.Abi);

                var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(contractHash);
                var contract = snapshot.GetAndChange(key)?.GetInteroperable<ContractState>();
                if (contract is null) throw new InvalidOperationException($"Updating Contract Does Not Exist: {contractHash}");
                if (!manifest.IsValid(contract.Hash)) throw new InvalidOperationException($"Invalid Manifest Hash: {contract.Hash}");

                contract.Nef = nefFile;
                contract.Manifest = manifest;
                contract.UpdateCounter++;

                OnDeploy(contract, snapshot, settings, true);
            }

            // following logic lifted from ContractManagement.OnDeploy
            static void OnDeploy(ContractState contract, DataCache snapshot, ProtocolSettings settings, bool update)
            {
                var deployMethod = contract.Manifest.Abi.GetMethod("_deploy", 2);
                if (deployMethod is not null)
                {
                    using (var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, null, settings))
                    {
                        var context = engine.LoadContract(contract, deployMethod, CallFlags.All);
                        context.EvaluationStack.Push(StackItem.Null);
                        context.EvaluationStack.Push(update ? StackItem.True : StackItem.False);
                        if (engine.Execute() != VMState.HALT) throw new InvalidOperationException("_deploy operation failed", engine.FaultException);
                    }
                }
            }

            // following logic lifted from ContractManagement.GetNextAvailableId
            static int GetNextAvailableId(DataCache snapshot)
            {
                const byte Prefix_NextAvailableId = 15;

                var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
                var item = snapshot.GetAndChange(key);
                int value = (int)(BigInteger)item;
                item.Add(1);
                return value;
            }

            // TODO: remove once https://github.com/neo-project/neo/pull/2396 is merged
            static void Check(byte[] script, ContractAbi abi)
            {
                Script s = new Script(script, true);
                foreach (ContractMethodDescriptor method in abi.Methods)
                    s.GetInstruction(method.Offset);
                abi.GetMethod(string.Empty, 0); // Trigger the construction of ContractAbi.methodDictionary to check the uniqueness of the method names.
                _ = abi.Events.ToDictionary(p => p.Name); // Check the uniqueness of the event names.
            }
        }

        // stripped down Blockchain.Persist logic to initize empty blockchain
        static void EnsureLedgerInitialized(IStore store, ProtocolSettings settings)
        {
            using var snapshot = new SnapshotCache(store.GetSnapshot());
            if (NativeContract.Ledger.Initialized(snapshot)) return;

            var block = NeoSystem.CreateGenesisBlock(settings);
            if (block.Transactions.Length != 0) throw new Exception("Unexpected Transactions in genesis block");

            using (var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, settings, 0))
            {
                using var sb = new ScriptBuilder();
                sb.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
                engine.LoadScript(sb.ToArray());
                if (engine.Execute() != VMState.HALT) throw new InvalidOperationException("NativeOnPersist operation failed", engine.FaultException);
            }

            using (var engine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, block, settings, 0))
            {
                using var sb = new ScriptBuilder();
                sb.EmitSysCall(ApplicationEngine.System_Contract_NativePostPersist);
                engine.LoadScript(sb.ToArray());
                if (engine.Execute() != VMState.HALT) throw new InvalidOperationException("NativePostPersist operation failed", engine.FaultException);
            }

            snapshot.Commit();
        }

        // following logic lifted from ApplicationEngine.CreateDummyBlock
        static Block CreateDummyBlock(IStore store, Transaction? tx = null)
        {
            using var snapshot = new SnapshotCache(store);
            UInt256 hash = NativeContract.Ledger.CurrentHash(snapshot);
            var currentBlock = NativeContract.Ledger.GetBlock(snapshot, hash);
            return new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = hash,
                    MerkleRoot = new UInt256(),
                    Timestamp = currentBlock.Timestamp + ProtocolSettings.Default.MillisecondsPerBlock,
                    Index = currentBlock.Index + 1,
                    NextConsensus = currentBlock.NextConsensus,
                    Witness = new Witness
                    {
                        InvocationScript = Array.Empty<byte>(),
                        VerificationScript = Array.Empty<byte>()
                    },
                },
                Transactions = tx == null ? Array.Empty<Transaction>() : new[] { tx }
            };
        }

        static Task<Script> CreateInvokeScriptAsync(Invocation invocation, string program, UInt160 scriptHash, ContractParameterParser paramParser)
        {
            return invocation.Match<Task<Script>>(
                invoke => paramParser.LoadInvocationScriptAsync(invoke.Path),
                oracle => Task.FromResult<Script>(OracleResponse.FixedScript),
                launch =>
                {
                    if (launch.Contract.Length > 0
                        && paramParser.TryLoadScriptHash(launch.Contract, out var hash))
                    {
                        scriptHash = hash;
                    }

                    var args = paramParser.ParseParameters(launch.Args).ToArray();
                    using var builder = new ScriptBuilder();
                    builder.EmitDynamicCall(scriptHash, launch.Operation, args);
                    return Task.FromResult<Script>(builder.ToArray());
                },
                deployment => Task.FromException<Script>(new Exception("CreateInvokeScriptAsync doesn't support ContractDeploymentInvocation")));
        }


        // static void UpdateContractStorage(IStore store, int contractId, Storages storages)
        // {
        //     using var snapshot = new SnapshotCache(store.GetSnapshot());
        //     foreach (var (key, item) in storages)
        //     {
        //         var storageKey = new StorageKey() { Id = contractId, Key = key };
        //         var updatedItem = snapshot.GetAndChange(storageKey);
        //         updatedItem.Value = item.Value;
        //     }
        //     snapshot.Commit();
        // }



        // static TransactionAttribute[] GetTransactionAttributes(Invocation invocation, IStore store, UInt160 contractHash)
        // {
        //     return invocation.Match(
        //         invoke => Array.Empty<TransactionAttribute>(),
        //         oracle => GetTransactionAttributes(oracle, store, contractHash),
        //         launch => Array.Empty<TransactionAttribute>());
        // }

        // static TransactionAttribute[] GetTransactionAttributes(OracleResponseInvocation invocation, IStore store, UInt160 contractHash, ContractParameterParser paramParser)
        // {
        //     var userData = invocation.UserData == null
        //         ? Neo.VM.Types.Null.Null
        //         : paramParser.ParseParameter(invocation.UserData).ToStackItem();

        //     using var snapshot = new SnapshotCache(store);

        //     // the following logic to create the OracleRequest record that drives the response process
        //     // is adapted from OracleContract.Request
        //     const byte Prefix_RequestId = 9;
        //     const byte Prefix_Request = 7;
        //     const int MaxUserDataLength = 512;

        //     StorageItem item_id = snapshot.GetAndChange(CreateStorageKey(Prefix_RequestId));
        //     ulong id = (ulong)(BigInteger)item_id;
        //     item_id.Add(1);

        //     snapshot.Add(
        //         CreateStorageKey(Prefix_Request).AddBigEndian(id),
        //         new StorageItem(
        //             new OracleRequest
        //             {
        //                 OriginalTxid = UInt256.Zero,
        //                 GasForResponse = invocation.GasForResponse,
        //                 Url = invocation.Url,
        //                 Filter = invocation.Filter,
        //                 CallbackContract = contractHash,
        //                 CallbackMethod = invocation.Callback,
        //                 UserData = BinarySerializer.Serialize(userData, MaxUserDataLength)
        //             }));

        //     snapshot.Commit();

        //     var response = new OracleResponse
        //     {
        //         Code = invocation.Code,
        //         Id = id,
        //         Result = Neo.Utility.StrictUTF8.GetBytes(Filter(invocation.Result, invocation.Filter))
        //     };

        //     return new TransactionAttribute[] { response };

        //     static Neo.SmartContract.KeyBuilder CreateStorageKey(byte prefix)
        //     {
        //         const int Oracle_ContractId = -4;
        //         return new Neo.SmartContract.KeyBuilder(Oracle_ContractId, prefix);
        //     }

        //     static string Filter(JToken json, string filterArgs)
        //     {
        //         if (string.IsNullOrEmpty(filterArgs))
        //             return json.ToString();

        //         JArray afterObjects = new JArray(json.SelectTokens(filterArgs, true));
        //         return afterObjects.ToString();
        //     }
        // }

        // static Task<Script> CreateInvokeScriptAsync(Invocation invocation, UInt160 scriptHash, ContractParameterParser paramParser)
        // {
        //     return invocation.Match<Task<Script>>(
        //         invoke => paramParser.LoadInvocationScriptAsync(invoke.Path),
        //         oracle => Task.FromResult<Script>(OracleResponse.FixedScript),
        //         launch =>
        //         {
        //             if (launch.Contract.Length > 0
        //                 && paramParser.TryLoadScriptHash(launch.Contract, out var hash))
        //             {
        //                 scriptHash = hash;
        //             }

        //             var args = paramParser.ParseParameters(launch.Args).ToArray();
        //             using var builder = new ScriptBuilder();
        //             builder.EmitDynamicCall(scriptHash, launch.Operation, args);
        //             return Task.FromResult<Script>(builder.ToArray());
        //         });
        // }

        // static IStore CreateBlockchainStorage(Dictionary<string, JToken> config)
        // {
        //     if (config.TryGetValue("checkpoint", out var checkpoint))
        //     {
        //         string checkpointTempPath;
        //         do
        //         {
        //             checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        //         }
        //         while (Directory.Exists(checkpointTempPath));

        //         var cleanup = AnonymousDisposable.Create(() =>
        //         {
        //             if (Directory.Exists(checkpointTempPath))
        //             {
        //                 Directory.Delete(checkpointTempPath);
        //             }
        //         });

        //         var (magic, addressVersion) = RocksDbStore.RestoreCheckpoint(checkpoint.Value<string>(), checkpointTempPath);
        //         // if (settings.Magic != magic)
        //         //     throw new Exception($"checkpoint magic ({magic}) doesn't match neo-express magic ({settings.Magic})");
        //         // if (settings.AddressVersion != addressVersion)
        //         //     throw new Exception($"checkpoint address version ({addressVersion}) doesn't match neo-express address version ({settings.AddressVersion})");

        //         return new CheckpointStore(
        //             RocksDbStore.OpenReadOnly(checkpointTempPath),
        //             cleanup);
        //     }
        //     else
        //     {
        //         return new MemoryStore();
        //     }
        // }

        private static (TriggerType trigger, Func<byte[], bool>? witnessChecker) ParseRuntime(ConfigProps config, byte addressVersion)
        {
            var hasCheckpoint = config.TryGetValue("checkpoint", out _);

            if (config.TryGetValue("runtime", out var token))
            {
                var trigger = "verification".Equals(token.Value<string>("trigger"), StringComparison.InvariantCultureIgnoreCase)
                    ? TriggerType.Verification : TriggerType.Application;

                var checkWitness = token["check-witness"];
                if (checkWitness?.Type == JTokenType.Boolean)
                {
                    return (trigger, _ => checkWitness.Value<bool>());
                }
                else if (checkWitness?.Type == JTokenType.Array)
                {
                    var witnesses = checkWitness.Select(t => ParseAddress(t.Value<string>(), addressVersion)).ToImmutableSortedSet();
                    return (trigger, hashOrPubkey => CheckWitness(hashOrPubkey, witnesses));
                }
                else if (checkWitness?.Type == JTokenType.String)
                {
                    if (checkWitness.Value<string>() != "checkpoint")
                    {
                        throw new Exception($"invalid check-witness value \"{checkWitness.Value<string>()}\"");
                    }

                    if (!hasCheckpoint)
                    {
                        throw new Exception("invalid launch config - checkpoint not specified");
                    }
                }

                return (trigger, DefaultWitnessChecker());
            }

            return (TriggerType.Application, DefaultWitnessChecker());

            Func<byte[], bool>? DefaultWitnessChecker() => hasCheckpoint ? (Func<byte[], bool>?)null : _ => true;

            static bool CheckWitness(byte[] hashOrPubkey, ImmutableSortedSet<UInt160> witnesses)
            {
                // hashOrPubkey parsing logic copied from ApplicationEngine.CheckWitness
                var hash = hashOrPubkey.Length switch
                {
                    20 => new UInt160(hashOrPubkey),
                    33 => Contract.CreateSignatureRedeemScript(ECPoint.DecodePoint(hashOrPubkey, ECCurve.Secp256r1)).ToScriptHash(),
                    _ => throw new ArgumentException()
                };
                return witnesses.Contains(hash);
            }
        }

        static IEnumerable<Signer> ParseSigners(ConfigProps config, byte addressVersion)
        {
            if (config.TryGetValue("signers", out var signers))
            {
                foreach (var signer in signers)
                {
                    if (signer.Type == JTokenType.String)
                    {
                        var account = ParseAddress(signer.Value<string>(), addressVersion);
                        yield return new Signer { Account = account, Scopes = WitnessScope.CalledByEntry };
                    }
                    else if (signer.Type == JTokenType.Object)
                    {
                        var account = ParseAddress(signer.Value<string>("account"), addressVersion);
                        var textScopes = signer.Value<string>("scopes");
                        var scopes = textScopes == null
                            ? WitnessScope.CalledByEntry
                            : (WitnessScope)Enum.Parse(typeof(WitnessScope), textScopes);
                        yield return new Signer { Account = account, Scopes = scopes };
                    }
                }
            }
            else
            {
                yield return new Signer { Account = UInt160.Zero, Scopes = WitnessScope.CalledByEntry };
            }
        }

        static UInt160 ParseAddress(string text, byte addressVersion) => (text[0] == '@')
            ? text[1..].ToScriptHash(addressVersion)
            : text.ToScriptHash(addressVersion);

        static async IAsyncEnumerable<DebugInfo> LoadDebugInfosAsync(ConfigProps config, IReadOnlyDictionary<string, string> sourceFileMap)
        {
            var program = ParseProgram(config);
            yield return await DebugInfoParser.LoadAsync(program, sourceFileMap).ConfigureAwait(false);

            // foreach (var (contractPath, _) in ParseStoredContracts())
            // {
            //     yield return await DebugInfoParser.Load(contractPath, sourceFileMap).ConfigureAwait(false);
            // }
        }

        // // IEnumerable<(string contractPath, Storages storages)> ParseStoredContracts()
        // // {
        // //     if (config.TryGetValue("stored-contracts", out var storedContracts))
        // //     {
        // //         foreach (var storedContract in storedContracts)
        // //         {
        // //             if (storedContract.Type == JTokenType.String)
        // //             {
        // //                 var path = storedContract.Value<string>();
        // //                 var storages = Enumerable.Empty<(byte[], StorageItem)>();
        // //                 yield return (path, storages);
        // //             }
        // //             else if (storedContract.Type == JTokenType.Object)
        // //             {
        // //                 var path = storedContract.Value<string>("program");
        // //                 var storages = ParseStorage(storedContract["storage"]);
        // //                 yield return (path, storages);
        // //             }
        // //             else
        // //             {
        // //                 throw new Exception("invalid stored-contract value");
        // //             }
        // //         }
        // //     }
        // // }


        // static Storages ParseStorage(Dictionary<string, JToken> config, ContractParameterParser paramParser)
        // {
        //     return config.TryGetValue("storage", out var token)
        //         ? ParseStorage(token, paramParser)
        //         : Enumerable.Empty<(byte[], StorageItem)>();
        // }

        // static Storages ParseStorage(JToken? token, ContractParameterParser paramParser)
        // {
        //     return token == null
        //         ? Enumerable.Empty<(byte[], StorageItem)>()
        //         : token.Select(t =>
        //             {
        //                 var key = ConvertParameter(t["key"], paramParser);
        //                 var item = new StorageItem
        //                 {
        //                     Value = ConvertParameter(t["value"], paramParser)
        //                 };
        //                 return (key, item);
        //             });

        //     static byte[] ConvertParameter(JToken? token, ContractParameterParser paramParser)
        //     {
        //         var arg = paramParser.ParseParameter(token ?? JValue.CreateNull());
        //         return arg.ToStackItem().GetSpan().ToArray();
        //     }
        // }

    }
}
