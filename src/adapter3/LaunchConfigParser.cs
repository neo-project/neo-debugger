using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    using Storages = IEnumerable<(byte[] key, StorageItem item)>;

    partial class LaunchConfigParser
    {
        public static async Task<IDebugSession> CreateDebugSessionAsync(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            var sourceFileMap = ImmutableDictionary<string, string>.Empty;
            if (launchArguments.ConfigurationProperties.TryGetValue("sourceFileMap", out var jsonSourceFileMap) && jsonSourceFileMap.Type == JTokenType.Object)
            {
                sourceFileMap = ((IEnumerable<KeyValuePair<string, JToken?>>)jsonSourceFileMap).ToImmutableDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Value<string>() ?? string.Empty);
            }

            var returnTypes = ImmutableList<CastOperation>.Empty;
            if (launchArguments.ConfigurationProperties.TryGetValue("return-types", out var jsonReturnTypes))
            {
                var builder = ImmutableList.CreateBuilder<CastOperation>();
                foreach (var returnType in jsonReturnTypes)
                {
                    builder.Add(DebugSession.CastOperations[returnType.Value<string>() ?? ""]);
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
                chain = fs.LoadChain(neoExpressPath.Value<string>() ?? "");
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


            var (storageProvider, settings) = LoadBlockchainStorage(config, chain?.Network, chain?.AddressVersion);
            using (var store = storageProvider.GetStore(null))
            {
                EnsureLedgerInitialized(store, settings);

                var (trigger, witnessChecker) = ParseRuntime(config, chain, settings.AddressVersion);
                if (trigger != TriggerType.Application)
                {
                    throw new Exception($"Trigger Type {trigger} not supported");
                }

                var signers = ParseSigners(config, chain, settings.AddressVersion).ToArray();

                Script invokeScript;
                var attributes = Array.Empty<TransactionAttribute>();
                if (invocation.IsT3) // T3 == ContractDeploymentInvocation
                {
                    if ((signers.Length == 0 || (signers.Length == 1 && signers[0].Account == UInt160.Zero))
                        && TryGetDeploymentSigner(config, chain, settings.AddressVersion, out var deploySigner))
                    {
                        signers = new[] { deploySigner };
                    }

                    using var builder = new ScriptBuilder();
                    builder.EmitDynamicCall(NativeContract.ContractManagement.Hash, "deploy", launchNefFile.ToArray(), launchManifest.ToJson().ToString());
                    invokeScript = builder.ToArray();
                }
                else
                {
                    var paramParser = CreateContractParameterParser(settings.AddressVersion, store, chain);
                    var deploySigner = TryGetDeploymentSigner(config, chain, settings.AddressVersion, out var _deploySigner)
                        ? _deploySigner
                        : new Signer { Account = UInt160.Zero };

                    var (lauchContractId, launchContractHash) = EnsureContractDeployed(store, launchNefFile, launchManifest, deploySigner, settings);
                    UpdateContractStorage(store, lauchContractId, ParseStorage(config, paramParser));
                    invokeScript = await CreateInvokeScriptAsync(invocation, program, launchContractHash, paramParser);

                    if (invocation.IsT1) // T1 == OracleResponseInvocation
                    {
                        attributes = GetTransactionAttributes(invocation.AsT1, store, launchContractHash, paramParser);
                    }
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
                    ValidUntilBlock = settings.MaxValidUntilBlockIncrement,
                    Attributes = attributes,
                    Witnesses = Array.Empty<Witness>()
                };

                var block = CreateDummyBlock(store, tx);

                var engine = new DebugApplicationEngine(tx, storageProvider, block, settings, witnessChecker);
                engine.LoadScript(invokeScript);
                return engine;
            }

            static bool TryGetDeploymentSigner(ConfigProps config, ExpressChain? chain, byte version, [MaybeNullWhen(false)] out Signer signer)
            {
                if (config.TryGetValue("deploy-signer", out var deploySignerToken))
                {
                    return TryParseSigner(deploySignerToken, chain, version, out signer);
                }

                signer = default;
                return false;
            }
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

        static (IDisposableStorageProvider storageProvider, ProtocolSettings settings) LoadBlockchainStorage(ConfigProps config, uint? magic = null, byte? addressVersion = null)
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

                var metadata = RocksDbStorageProvider.RestoreCheckpoint(checkpoint.Value<string>() ?? "", checkpointTempPath);
                if (magic.HasValue && magic.Value != metadata.magic)
                    throw new Exception($"checkpoint magic ({metadata.magic}) doesn't match ({magic.Value})");
                if (addressVersion.HasValue && addressVersion.Value != metadata.addressVersion)
                    throw new Exception($"checkpoint address version ({metadata.addressVersion}) doesn't match ({addressVersion.Value})");

                var storageProvider = new CheckpointStorageProvider(RocksDbStorageProvider.OpenReadOnly(checkpointTempPath), checkpointCleanup: cleanup);
                var settings = ProtocolSettings.Default with
                {
                    Network = metadata.magic,
                    AddressVersion = metadata.addressVersion
                };

                return (storageProvider, settings);
            }
            else
            {
                var settings = ProtocolSettings.Default with
                {
                    Network = magic.HasValue ? magic.Value : ProtocolSettings.Default.Network,
                    AddressVersion = addressVersion.HasValue ? addressVersion.Value : ProtocolSettings.Default.AddressVersion
                };
                return (new MemoryStorageProvider(), settings);
            }
        }

        class MemoryStorageProvider : IDisposableStorageProvider
        {
            MemoryStore defaultStore = new MemoryStore();
            ConcurrentDictionary<string, MemoryStore> storages = new ConcurrentDictionary<string, MemoryStore>();

            public void Dispose()
            {
                foreach (var (key, value) in storages)
                {
                    value.Dispose();
                }
            }

            public IStore GetStore(string? path) => path == null ? defaultStore : storages.GetOrAdd(path, _ => new MemoryStore());
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

        static (int id, UInt160 scriptHash) EnsureContractDeployed(IStore store, NefFile nefFile, ContractManifest manifest, Signer deploySigner, ProtocolSettings settings)
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
                            Update(snapshot, deploySigner, contract.Hash, nefFile, manifest, settings);
                            snapshot.Commit();
                        }

                        return (contract.Id, contract.Hash);
                    }
                }
            }

            // if no existing contract with matching manifest.Name is found, deploy the contract
            using (var snapshot = new SnapshotCache(store.GetSnapshot()))
            {
                var contractState = Deploy(snapshot, deploySigner, nefFile, manifest, settings);
                snapshot.Commit();

                return (contractState.Id, contractState.Hash);
            }

            // following logic lifted from ContractManagement.Deploy
            static ContractState Deploy(DataCache snapshot, Signer deploySigner, NefFile nefFile, ContractManifest manifest, ProtocolSettings settings)
            {
                Neo.SmartContract.Helper.Check(nefFile.Script, manifest.Abi);

                var hash = Neo.SmartContract.Helper.GetContractHash(deploySigner.Account, nefFile.CheckSum, manifest.Name);
                var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(hash);

                if (snapshot.Contains(key)) throw new InvalidOperationException($"Contract Already Exists: {hash}");
                if (!manifest.IsValid(hash)) throw new InvalidOperationException($"Invalid Manifest Hash: {hash}");

                var contract = new ContractState
                {
                    Hash = hash,
                    Id = GetNextAvailableId(snapshot),
                    Manifest = manifest,
                    Nef = nefFile,
                    UpdateCounter = 0,
                };

                snapshot.Add(key, new StorageItem(contract));
                OnDeploy(contract, deploySigner, snapshot, settings, false);
                return contract;
            }

            // following logic lifted from ContractManagement.Update
            static void Update(DataCache snapshot, Signer deploySigner, UInt160 contractHash, NefFile nefFile, ContractManifest manifest, ProtocolSettings settings)
            {
                Neo.SmartContract.Helper.Check(nefFile.Script, manifest.Abi);

                var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(contractHash);
                var contract = snapshot.GetAndChange(key)?.GetInteroperable<ContractState>();
                if (contract is null) throw new InvalidOperationException($"Updating Contract Does Not Exist: {contractHash}");
                if (!manifest.IsValid(contract.Hash)) throw new InvalidOperationException($"Invalid Manifest Hash: {contract.Hash}");

                contract.Nef = nefFile;
                contract.Manifest = manifest;
                contract.UpdateCounter++;

                OnDeploy(contract, deploySigner, snapshot, settings, true);
            }

            // following logic lifted from ContractManagement.OnDeploy
            static void OnDeploy(ContractState contract, Signer deploySigner, DataCache snapshot, ProtocolSettings settings, bool update)
            {
                var deployMethod = contract.Manifest.Abi.GetMethod("_deploy", 2);
                if (deployMethod is not null)
                {
                    var tx = new Transaction
                    {
                        Attributes = Array.Empty<TransactionAttribute>(),
                        Script = Array.Empty<byte>(),
                        Signers = new[] { deploySigner },
                        Witnesses = Array.Empty<Witness>()
                    };

                    using (var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshot, null, settings))
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
        }

        static void UpdateContractStorage(IStore store, int contractId, Storages storages)
        {
            using var snapshot = new SnapshotCache(store.GetSnapshot());
            foreach (var (key, storageItem) in storages)
            {
                var storageKey = new StorageKey() { Id = contractId, Key = key };
                var item = snapshot.GetAndChange(storageKey);
                if (item == null)
                {
                    snapshot.Add(storageKey, new StorageItem() { Value = storageItem.Value });
                }
                else
                {
                    item.Value = storageItem.Value;
                }
            }
            snapshot.Commit();
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

        static TransactionAttribute[] GetTransactionAttributes(in OracleResponseInvocation invocation, IStore store, UInt160 contractHash, ContractParameterParser paramParser)
        {
            var (requestId, request) = GetOracleRequestId(store, invocation, contractHash, paramParser);
            var response = new OracleResponse
            {
                Code = invocation.Code,
                Id = requestId,
                Result = Filter(request.Filter, invocation.Result)
            };
            return new TransactionAttribute[] { response };

            static byte[] Filter(string filter, string result)
            {
                if (string.IsNullOrEmpty(filter))
                    return Neo.Utility.StrictUTF8.GetBytes(result);

                var beforeObject = Newtonsoft.Json.Linq.JObject.Parse(result);
                var afterObjects = new Newtonsoft.Json.Linq.JArray(beforeObject.SelectTokens(filter));
                return Neo.Utility.StrictUTF8.GetBytes(afterObjects.ToString(Newtonsoft.Json.Formatting.None));
            }

            static (ulong requestId, OracleRequest request) GetOracleRequestId(IStore store, in OracleResponseInvocation invocation, UInt160 contractHash, ContractParameterParser paramParser)
            {
                // first, look to see if there's an outstanding request for this URL/contract 
                using (var snapshotView = new SnapshotCache(store))
                {
                    foreach (var (requestId, request) in NativeContract.Oracle.GetRequestsByUrl(snapshotView, invocation.Url))
                    {
                        if (request.CallbackContract == contractHash)
                        {
                            return (requestId, request);
                        }
                    }
                }

                // If there's no outstanding request for this URL/contract, create one
                // following logic lifted from OracleContract.Request
                var userData = invocation.UserData == null
                    ? Neo.VM.Types.Null.Null
                    : paramParser.ParseParameter(invocation.UserData).ToStackItem();

                using (var snapshot = new SnapshotCache(store.GetSnapshot()))
                {
                    const byte Prefix_IdList = 6;
                    const byte Prefix_Request = 7;
                    const byte Prefix_RequestId = 9;
                    const int MaxCallbackLength = 32;
                    const int MaxFilterLength = 128;
                    const int MaxUrlLength = 256;
                    const int MaxUserDataLength = 512;

                    if (Neo.Utility.StrictUTF8.GetByteCount(invocation.Url) > MaxUrlLength
                        || Neo.Utility.StrictUTF8.GetByteCount(invocation.Filter) > MaxFilterLength
                        || Neo.Utility.StrictUTF8.GetByteCount(invocation.Callback) > MaxCallbackLength
                        || invocation.Callback.StartsWith('_'))
                    {
                        throw new ArgumentException();
                    }

                    var idKey = new KeyBuilder(NativeContract.Oracle.Id, Prefix_RequestId);
                    var item_id = snapshot.GetAndChange(idKey);
                    ulong requestId = (ulong)(BigInteger)item_id;
                    item_id.Add(1);

                    var requestKey = new KeyBuilder(NativeContract.Oracle.Id, Prefix_Request).AddBigEndian(requestId);
                    var request = new OracleRequest
                    {
                        OriginalTxid = UInt256.Zero,
                        GasForResponse = invocation.GasForResponse,
                        Url = invocation.Url,
                        CallbackContract = contractHash,
                        CallbackMethod = invocation.Callback,
                        Filter = invocation.Filter,
                        UserData = BinarySerializer.Serialize(userData, MaxUserDataLength)
                    };
                    snapshot.Add(requestKey, new StorageItem(request));

                    var urlHash = Neo.Cryptography.Crypto.Hash160(Neo.Utility.StrictUTF8.GetBytes(invocation.Url));
                    var listKey = new KeyBuilder(NativeContract.Oracle.Id, Prefix_IdList).Add(urlHash);
                    var list = snapshot.GetAndChange(listKey, () => new StorageItem(new IdList())).GetInteroperable<IdList>();
                    if (list.Count >= 256)
                        throw new InvalidOperationException("There are too many pending responses for this url");
                    list.Add(requestId);

                    snapshot.Commit();
                    return (requestId, request);
                }
            }
        }

        class IdList : List<ulong>, IInteroperable
        {
            public void FromStackItem(StackItem stackItem)
            {
                foreach (StackItem item in (Neo.VM.Types.Array)stackItem)
                    Add((ulong)item.GetInteger());
            }

            public StackItem ToStackItem(ReferenceCounter referenceCounter)
            {
                return new Neo.VM.Types.Array(referenceCounter, this.Select(p => (Neo.VM.Types.Integer)p));
            }
        }

        static Storages ParseStorage(ConfigProps config, ContractParameterParser paramParser)
            => config.TryGetValue("storage", out var token)
                ? ParseStorage(token, paramParser)
                : Enumerable.Empty<(byte[], StorageItem)>();

        static Storages ParseStorage(JToken? token, ContractParameterParser paramParser)
        {
            return token == null
                ? Enumerable.Empty<(byte[], StorageItem)>()
                : token.Select(t =>
                    {
                        var key = ConvertParameter(t["key"], paramParser);
                        var item = new StorageItem
                        {
                            Value = ConvertParameter(t["value"], paramParser)
                        };
                        return (key, item);
                    });

            static byte[] ConvertParameter(JToken? token, ContractParameterParser paramParser)
            {
                var arg = paramParser.ParseParameter(token ?? JValue.CreateNull());
                return arg.ToStackItem().GetSpan().ToArray();
            }
        }

        static (TriggerType trigger, Func<byte[], bool>? witnessChecker) ParseRuntime(ConfigProps config, ExpressChain? chain, byte version)
        {
            if (config.TryGetValue("runtime", out var jsonRuntime))
            {
                var trigger = "verification".Equals(jsonRuntime.Value<string>("trigger"), StringComparison.OrdinalIgnoreCase)
                    ? TriggerType.Verification : TriggerType.Application;

                var jsonWitnesses = jsonRuntime["witnesses"];
                if (jsonWitnesses?.Type == JTokenType.Object)
                {
                    var checkResult = jsonWitnesses.Value<bool>("check-result");
                    return (trigger, _ => checkResult);
                }
                else if (jsonWitnesses?.Type == JTokenType.Array)
                {
                    var witnesses = jsonWitnesses.Select(t => ParseAddress(t.Value<string>() ?? "", chain, version)).ToImmutableSortedSet();
                    return (trigger, hashOrPubkey => CheckWitness(hashOrPubkey, witnesses));
                }

                return (trigger, _ => true);
            }

            return (TriggerType.Application, _ => true);

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

        static bool TryParseSigner(JToken token, ExpressChain? chain, byte version, [MaybeNullWhen(false)] out Signer signer)
        {
            if (token.Type == JTokenType.String)
            {
                var account = ParseAddress(token.Value<string>() ?? "", chain, version);
                signer = new Signer { Account = account, Scopes = WitnessScope.CalledByEntry };
                return true;
            }

            if (token.Type == JTokenType.Object)
            {
                var account = ParseAddress(token.Value<string>("account") ?? "", chain, version);
                var scopes = token.Value<string>("scopes");
                var allowedContracts = token["allowedcontracts"]?.Select(j => UInt160.Parse(j.Value<string>())).ToArray();
                var allowedGropus = token["allowedgroups"]?.Select(j => ECPoint.Parse(j.Value<string>(), ECCurve.Secp256r1)).ToArray();

                signer = new Signer
                {
                    Account = account,
                    Scopes = string.IsNullOrEmpty(scopes) ? WitnessScope.CalledByEntry : Enum.Parse<WitnessScope>(scopes),
                    AllowedContracts = allowedContracts,
                    AllowedGroups = allowedGropus,
                };
                return true;
            }

            signer = default;
            return false;
        }

        static IEnumerable<Signer> ParseSigners(ConfigProps config, ExpressChain? chain, byte version)
        {
            if (config.TryGetValue("signers", out var signersJson))
            {
                foreach (var token in signersJson)
                {
                    if (TryParseSigner(token, chain, version, out var signer))
                    {
                        yield return signer;
                    }
                }
            }
            else
            {
                yield return new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None };
            }
        }

        static UInt160 ParseAddress(string text, ExpressChain? chain, byte version)
        {
            text = text[0] == '@' ? text[1..] : text;

            if (chain != null)
            {
                if (chain.Wallets != null && chain.Wallets.Count > 0)
                {
                    for (int i = 0; i < chain.Wallets.Count; i++)
                    {
                        if (string.Equals(text, chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase)
                            && TryToScriptHash(chain.Wallets[i].DefaultAccount?.ScriptHash ?? string.Empty, version, out var scriptHash))
                        {
                            return scriptHash;
                        }
                    }
                }

                Debug.Assert(chain.ConsensusNodes != null && chain.ConsensusNodes.Count > 0);

                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    var nodeWallet = chain.ConsensusNodes[i].Wallet;
                    if (string.Equals(text, nodeWallet.Name, StringComparison.OrdinalIgnoreCase)
                        && TryToScriptHash(nodeWallet.DefaultAccount?.ScriptHash ?? string.Empty, version, out var scriptHash))
                    {
                        return scriptHash;
                    }
                }

                if (string.Equals(text, "genesis", StringComparison.OrdinalIgnoreCase))
                {
                    return GetGenesisScriptHash(chain, version);
                }
            }

            if (TryToScriptHash(text, version, out var hash))
            {
                return hash;
            }

            throw new FormatException();

            static bool TryToScriptHash(string address, byte version, [NotNullWhen(true)] out UInt160? scriptHash)
            {
                byte[] data = Neo.Cryptography.Base58.Base58CheckDecode(address);
                if (data.Length == 21 && data[0] == version)
                {
                    scriptHash = new UInt160(data.AsSpan(1));
                    return true;
                }

                scriptHash = default;
                return false;
            }

            static UInt160 GetGenesisScriptHash(ExpressChain chain, byte version)
            {
                Debug.Assert(chain.ConsensusNodes != null && chain.ConsensusNodes.Count > 0);

                var nodeWallet = chain.ConsensusNodes[0].Wallet;
                for (int i = 0; i < nodeWallet.Accounts.Count; i++)
                {
                    var account = nodeWallet.Accounts[i];
                    if (account.Contract.Script.HexToBytes().IsMultiSigContract())
                    {
                        return account.ScriptHash.ToScriptHash(version);
                    }
                }

                throw new FormatException();
            }
        }

        static async IAsyncEnumerable<DebugInfo> LoadDebugInfosAsync(ConfigProps config, IReadOnlyDictionary<string, string> sourceFileMap)
        {
            var program = ParseProgram(config);
            var debugInfo = (await DebugInfo.LoadAsync(program, sourceFileMap).ConfigureAwait(false))
                .Match(di => di, _ => throw new FileNotFoundException(program));
            yield return debugInfo;
        }
    }
}
