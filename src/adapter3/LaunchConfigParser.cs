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

using Script = Neo.VM.Script;

namespace NeoDebug.Neo3
{
    using Invocation = OneOf.OneOf<LaunchConfigParser.InvokeFileInvocation, LaunchConfigParser.OracleResponseInvocation, LaunchConfigParser.LaunchInvocation>;
    using Storages = IEnumerable<(byte[] key, StorageItem item)>;

    partial class LaunchConfigParser
    {
        readonly Dictionary<string, JToken> config;
        readonly ExpressChain? chain;
        readonly byte addressVersion;
        readonly Dictionary<string, UInt160> contracts = new Dictionary<string, UInt160>();
        readonly Dictionary<string, UInt160> accounts = new Dictionary<string, UInt160>();
        readonly ContractParameterParser paramParser;

        LaunchConfigParser(LaunchArguments launchArguments)
        {
            config = launchArguments.ConfigurationProperties;

            addressVersion = ProtocolSettings.Default.AddressVersion;
            chain = CreateExpressChain(config);

            if (chain != null)
            {
                addressVersion = chain.AddressVersion;
            }
            else if (config.TryGetValue("address-version", out var addressVersionJson))
            {
                addressVersion = addressVersionJson.Value<byte>();
            }

            paramParser = new ContractParameterParser(addressVersion, accounts.TryGetValue, contracts.TryGetValue);

            static ExpressChain? CreateExpressChain(Dictionary<string, JToken> config)
            {
                if (config.TryGetValue("neo-express", out var neoExpressPath))
                {
                    var fs = new System.IO.Abstractions.FileSystem();
                    return fs.LoadChain(neoExpressPath.Value<string>());
                }

                return null;
            }
        }

        public static Task<IDebugSession> CreateDebugSessionAsync(LaunchArguments launchArguments, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            var launchParser = new LaunchConfigParser(launchArguments);
            return launchParser.CreateDebugSessionAsync(sendEvent, defaultDebugView);
        }

        async Task<IDebugSession> CreateDebugSessionAsync(Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            var sourceFileMap = ParseSourceFileMap(config);
            var debugInfoList = await LoadDebugInfosAsync(sourceFileMap).ToListAsync().ConfigureAwait(false);
            var returnTypes = ParseReturnTypes(config).ToList();
            var engine = await CreateEngineAsync().ConfigureAwait(false);
            return new DebugSession(engine, debugInfoList, returnTypes, sendEvent, defaultDebugView);
        }

        async Task<IApplicationEngine> CreateEngineAsync()
        {
            if (config.TryGetValue("invocation", out var json))
            {
                if (json["traceFile"] != null)
                {
                    return CreateTraceEngine(json.Value<string>("traceFile"));
                }

                if (TryGetInvocation(json, out var invocation))
                {
                    return await CreateDebugEngineAsync(invocation).ConfigureAwait(false);
                }
            }

            throw new JsonException("invalid invocation config");

            static bool TryGetInvocation(JToken json, out Invocation result)
            {
                if (json["invokeFile"] != null)
                {
                    result = new InvokeFileInvocation(json.Value<string>("invokeFile"));
                    return true;
                }

                if (OracleResponseInvocation.TryFromJson(json["oracleResponse"], out var oracleInvocation))
                {
                    result = oracleInvocation;
                    return true;
                }

                if (LaunchInvocation.TryFromJson(json, out var launchInvocation))
                {
                    result = launchInvocation;
                    return true;
                }

                result = default;
                return false;
            }
        }

        IApplicationEngine CreateTraceEngine(string traceFilePath)
        {
            var contracts = new List<NefFile>();
            var launchContractPath = config["program"].Value<string>() ?? throw new Exception("missing program config property");
            var launchContract = LoadContract(launchContractPath);
            contracts.Add(launchContract);

            // TODO: load other contracts

            return new TraceApplicationEngine(traceFilePath, contracts);
        }

        async Task<IApplicationEngine> CreateDebugEngineAsync(Invocation invocation)
        {
            var (trigger, witnessChecker) = ParseRuntime(config, addressVersion);
            if (trigger != TriggerType.Application)
            {
                throw new Exception($"Trigger Type {trigger} not supported");
            }

            var launchContractPath = config["program"].Value<string>() ?? throw new Exception("missing program config property");
            var launchContract = LoadContract(launchContractPath);
            var launchContractManifest = await LoadManifestAsync(launchContractPath);

            var store = CreateBlockchainStorage(config, chain);
            EnsureLedgerInitialized(store, chain);

            // ParseSigners accesses ProtocolSettings.Default so it needs to be after CreateBlockchainStorage
            var signers = ParseSigners(config, addressVersion).ToArray();
            var (lauchContractId, launchContractHash) = AddContract(store, launchContract, launchContractManifest, signers);

            // TODO: load other contracts
            //          Not sure supporting other contracts is a good idea anymore. Since there's no way to calcualte the 
            //          contract id hash prior to deployment in Neo 3, I'm thinking the better approach would be to simply
            //          deploy whatever contracts you want and take a snapshot rather than deploying multiple contracts 
            //          during launch configuration.

            var settings = ProtocolSettings.Default with 
            {
                Magic = chain?.Magic ?? ProtocolSettings.Default.Magic,
                AddressVersion = addressVersion
            };

            Block dummyBlock;
            {
                // cache the deployed contracts for use by parameter parser
                using var snapshot = new SnapshotCache(store);
                foreach (var contractState in NativeContract.ContractManagement.ListContracts(snapshot))
                {
                    contracts.TryAdd(contractState.Manifest.Name, contractState.Hash);
                }

                dummyBlock = CreateDummyBlock(snapshot, settings);
            }

            UpdateContractStorage(store, lauchContractId, ParseStorage());

            // TODO: load other contract storage (unless we cut this feature - see comment above)

            var invokeScript = await CreateInvokeScriptAsync(invocation, launchContractHash).ConfigureAwait(false);

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = invokeScript,
                Signers = signers,
                ValidUntilBlock = Transaction.MaxValidUntilBlockIncrement,
                Attributes = GetTransactionAttributes(invocation, store, launchContractHash),
                Witnesses = Array.Empty<Witness>()
            };

            var engine = new DebugApplicationEngine(tx, new SnapshotCache(store), dummyBlock, settings, witnessChecker);
            engine.LoadScript(invokeScript);
            return engine;

            static (int id, UInt160 scriptHash) AddContract(IStore store, NefFile contract, ContractManifest manifest, Signer[] signers)
            {
                // TODO: Can we refactor NativeContract.Management to support contract check, add and update from the debugger
                {
                    // logic duplicated from ContractManagement.Check
                    Script s = new Script(contract.Script, true);
                    var abi = manifest.Abi;
                    foreach (ContractMethodDescriptor method in abi.Methods)
                        s.GetInstruction(method.Offset);
                    abi.GetMethod(string.Empty, 0); // Trigger the construction of ContractAbi.methodDictionary to check the uniqueness of the method names.
                    _ = abi.Events.ToDictionary(p => p.Name); // Check the uniqueness of the event names.
                }

                {
                    using var storageView = new SnapshotCache(store);
                    // check to see if there's a contract with a name that matches the name in the manifest
                    foreach (var contractState in NativeContract.ContractManagement.ListContracts(storageView))
                    {
                        // do not update native contracts, even if names match
                        if (contractState.Id <= 0) continue;

                        if (string.Equals(contractState.Manifest.Name, manifest.Name))
                        {
                            // if the deployed script doesn't match the script parameter, overwrite the deployed script
                            if (contract.Script.ToScriptHash() != contractState.Script.ToScriptHash())
                            {
                                const byte Prefix_Contract = 8;

                                using var snapshot = new SnapshotCache(store.GetSnapshot());

                                var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(contractState.Hash);
                                var updateContractState = snapshot.GetAndChange(key)?.GetInteroperable<ContractState>();
                                if (updateContractState is null) throw new InvalidOperationException($"Updating Contract Does Not Exist: {contractState.Hash}");
                                updateContractState.Nef = contract;
                                updateContractState.Manifest = manifest;

                                snapshot.Commit();
                            }

                            return (contractState.Id, contractState.Hash);
                        }
                    }
                }

                {
                    // if no existing contract with matching manifest.Name is found, 
                    // add the provided contract to storage

                    using var snapshot = new SnapshotCache(store.GetSnapshot());
                    var tx = new Transaction()
                    {
                        Signers = signers.Length == 0 ? new[] { new Signer() { Account = UInt160.Zero } } : signers
                    };

                    var contractState = new ContractState();
                    using (ApplicationEngine engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshot))
                    {
                        using var sb = new ScriptBuilder();
                        sb.EmitDynamicCall(NativeContract.ContractManagement.Hash, "deploy", contract.ToArray(), manifest.ToJson().ToString());
                        engine.LoadScript(sb.ToArray());
                        if (engine.Execute() != VMState.HALT) throw new InvalidOperationException("deploy operation failed");
                        if (engine.ResultStack.Count < 1) throw new InvalidOperationException("deploy operation returned invalid results");
                        ((IInteroperable)contractState).FromStackItem(engine.ResultStack.Peek());
                    }

                    snapshot.Commit();
                    return (contractState.Id, contractState.Hash);
                }
            }

            // stripped down Blockchain.Persist logic to initize empty blockchain
            static void EnsureLedgerInitialized(IStore store, ExpressChain? chain)
            {
                using var snapshot = new SnapshotCache(store.GetSnapshot());
                if (NativeContract.Ledger.Initialized(snapshot)) return;

                var settings = GetProtocolSettings(chain);
                var block = CreateGenesisBlock(settings);
                if (block.Transactions.Length != 0) throw new Exception("Unexpected Transactions in genesis block");

                using (ApplicationEngine engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, settings, 0))
                {
                    using var sb = new ScriptBuilder();
                    sb.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
                    engine.LoadScript(sb.ToArray());
                    if (engine.Execute() != VMState.HALT) throw new InvalidOperationException("NativeOnPersist operation failed");
                }

                using (ApplicationEngine engine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, block))
                {
                    using var sb = new ScriptBuilder();
                    sb.EmitSysCall(ApplicationEngine.System_Contract_NativePostPersist);
                    engine.LoadScript(sb.ToArray());
                    if (engine.Execute() != VMState.HALT) throw new InvalidOperationException("NativePostPersist operation failed");
                }

                snapshot.Commit();
            }

            // duplicated from ApplicationEngine.CreateDummyBlock
            static Block CreateDummyBlock(DataCache snapshot, ProtocolSettings settings)
            {
                UInt256 hash = NativeContract.Ledger.CurrentHash(snapshot);
                Block currentBlock = NativeContract.Ledger.GetBlock(snapshot, hash);
                return new Block
                {
                    Header = new Header
                    {
                        Version = 0,
                        PrevHash = hash,
                        MerkleRoot = new UInt256(),
                        Timestamp = currentBlock.Timestamp + settings.MillisecondsPerBlock,
                        Index = currentBlock.Index + 1,
                        NextConsensus = currentBlock.NextConsensus,
                        Witness = new Witness
                        {
                            InvocationScript = Array.Empty<byte>(),
                            VerificationScript = Array.Empty<byte>()
                        },
                    },
                    Transactions = Array.Empty<Transaction>()
                };
            }

            // TODO: move to lib-bctk
            static ProtocolSettings GetProtocolSettings(ExpressChain? chain, uint secondsPerBlock = 0)
            {
                return chain == null ? ProtocolSettings.Default : ProtocolSettings.Default with {
                    Magic = chain.Magic,
                    AddressVersion = chain.AddressVersion,
                    MillisecondsPerBlock = secondsPerBlock == 0 ? 15000 : secondsPerBlock * 1000,
                    ValidatorsCount = chain.ConsensusNodes.Count,
                    StandbyCommittee = chain.ConsensusNodes.Select(GetPublicKey).ToArray(),
                    SeedList = chain.ConsensusNodes
                        .Select(n => $"{System.Net.IPAddress.Loopback}:{n.TcpPort}")
                        .ToArray(),
                };

                static ECPoint GetPublicKey(ExpressConsensusNode node)
                    => new KeyPair(node.Wallet.Accounts.Select(a => a.PrivateKey).Distinct().Single().HexToBytes()).PublicKey;
            }

            // TODO: remove once https://github.com/neo-project/neo/pull/2381 is merged
            static Block CreateGenesisBlock(ProtocolSettings settings)
            {
                return new Block
                {
                    Header = new Header
                    {
                        PrevHash = UInt256.Zero,
                        MerkleRoot = UInt256.Zero,
                        Timestamp = (new DateTime(2016, 7, 15, 15, 8, 21, DateTimeKind.Utc)).ToTimestampMS(),
                        Index = 0,
                        PrimaryIndex = 0,
                        NextConsensus = Contract.GetBFTAddress(settings.StandbyValidators),
                        Witness = new Witness
                        {
                            InvocationScript = Array.Empty<byte>(),
                            VerificationScript = new[] { (byte)OpCode.PUSH1 }
                        },
                    },
                    Transactions = Array.Empty<Transaction>()
                };
            }
        }

        private static void UpdateContractStorage(IStore store, int contractId, Storages storages)
        {
            using var snapshot = new SnapshotCache(store.GetSnapshot());
            foreach (var (key, item) in storages)
            {
                var storageKey = new StorageKey() { Id = contractId, Key = key };
                var updatedItem = snapshot.GetAndChange(storageKey);
                updatedItem.Value = item.Value;
            }
            snapshot.Commit();
        }

        private static NefFile LoadContract(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, false);
            return reader.ReadSerializable<NefFile>();
        }

        private static async Task<ContractManifest> LoadManifestAsync(string contractPath)
        {
            var manifestBytes = await File.ReadAllBytesAsync(Path.ChangeExtension(contractPath, ".manifest.json")).ConfigureAwait(false);
            return ContractManifest.Parse(manifestBytes);
        }

        private static TransactionAttribute[] GetTransactionAttributes(Invocation invocation, IStore store, UInt160 contractHash)
        {
            return invocation.Match(
                invoke => Array.Empty<TransactionAttribute>(),
                oracle => GetTransactionAttributes(oracle, store, contractHash),
                launch => Array.Empty<TransactionAttribute>());
        }

        TransactionAttribute[] GetTransactionAttributes(OracleResponseInvocation invocation, IStore store, UInt160 contractHash)
        {
            var userData = invocation.UserData == null
                ? Neo.VM.Types.Null.Null
                : paramParser.ParseParameter(invocation.UserData).ToStackItem();

            using var snapshot = new SnapshotCache(store);

            // the following logic to create the OracleRequest record that drives the response process
            // is adapted from OracleContract.Request
            const byte Prefix_RequestId = 9;
            const byte Prefix_Request = 7;
            const int MaxUserDataLength = 512;

            StorageItem item_id = snapshot.GetAndChange(CreateStorageKey(Prefix_RequestId));
            ulong id = (ulong)(BigInteger)item_id;
            item_id.Add(1);

            snapshot.Add(
                CreateStorageKey(Prefix_Request).AddBigEndian(id), 
                new StorageItem(
                    new OracleRequest
                    {
                        OriginalTxid = UInt256.Zero,
                        GasForResponse = invocation.GasForResponse,
                        Url = invocation.Url,
                        Filter = invocation.Filter,
                        CallbackContract = contractHash,
                        CallbackMethod = invocation.Callback,
                        UserData = BinarySerializer.Serialize(userData, MaxUserDataLength)
                    }));

            snapshot.Commit();

            var response = new OracleResponse
            {
                Code = invocation.Code,
                Id = id,
                Result = Neo.Utility.StrictUTF8.GetBytes(Filter(invocation.Result, invocation.Filter))
            };

            return new TransactionAttribute[] { response };

            static Neo.SmartContract.KeyBuilder CreateStorageKey(byte prefix)
            {
                const int Oracle_ContractId = -4;
                return new Neo.SmartContract.KeyBuilder(Oracle_ContractId, prefix);
            }

            static string Filter(JToken json, string filterArgs)
            {
                if (string.IsNullOrEmpty(filterArgs))
                    return json.ToString();

                JArray afterObjects = new JArray(json.SelectTokens(filterArgs, true));
                return afterObjects.ToString();
            }
        }

        Task<Script> CreateInvokeScriptAsync(Invocation invocation, UInt160 scriptHash)
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
                });
        }

        private static IStore CreateBlockchainStorage(Dictionary<string, JToken> config, ExpressChain? chain)
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

                var checkpointMagic = RocksDbStore.RestoreCheckpoint(checkpoint.Value<string>(), checkpointTempPath);
                if (chain != null && chain.Magic != checkpointMagic)
                {
                    throw new Exception($"checkpoint magic ({checkpointMagic}) doesn't match neo-express magic ({chain.Magic})");
                }

                return new CheckpointStore(
                    RocksDbStore.OpenReadOnly(checkpointTempPath),
                    cleanup);
            }
            else
            {
                return new MemoryStore();
            }
        }

        private static (TriggerType trigger, Func<byte[], bool>? witnessChecker) ParseRuntime(Dictionary<string, JToken> config, byte addressVersion)
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

        private static IEnumerable<Signer> ParseSigners(Dictionary<string, JToken> config, byte addressVersion)
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
        }

        private static UInt160 ParseAddress(string text, byte addressVersion)
        {
            if (text[0] == '@')
            {
                return text[1..].ToScriptHash(addressVersion);
            }

            return text.ToScriptHash(addressVersion);
        }

        async IAsyncEnumerable<DebugInfo> LoadDebugInfosAsync(IReadOnlyDictionary<string, string> sourceFileMap)
        {
            yield return await DebugInfoParser.Load(config["program"].Value<string>(), sourceFileMap).ConfigureAwait(false);

            foreach (var (contractPath, _) in ParseStoredContracts())
            {
                yield return await DebugInfoParser.Load(contractPath, sourceFileMap).ConfigureAwait(false);
            }
        }

        IEnumerable<(string contractPath, Storages storages)> ParseStoredContracts()
        {
            if (config.TryGetValue("stored-contracts", out var storedContracts))
            {
                foreach (var storedContract in storedContracts)
                {
                    if (storedContract.Type == JTokenType.String)
                    {
                        var path = storedContract.Value<string>();
                        var storages = Enumerable.Empty<(byte[], StorageItem)>();
                        yield return (path, storages);
                    }
                    else if (storedContract.Type == JTokenType.Object)
                    {
                        var path = storedContract.Value<string>("program");
                        var storages = ParseStorage(storedContract["storage"]);
                        yield return (path, storages);
                    }
                    else
                    {
                        throw new Exception("invalid stored-contract value");
                    }
                }
            }
        }

        private static IEnumerable<string> ParseReturnTypes(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("return-types", out var returnTypes))
            {
                foreach (var returnType in returnTypes)
                {
                    yield return VariableManager.CastOperations[returnType.Value<string>()];
                }
            }
        }

        Storages ParseStorage()
        {
            return config.TryGetValue("storage", out var token)
                ? ParseStorage(token)
                : Enumerable.Empty<(byte[], StorageItem)>();
        }

        Storages ParseStorage(JToken? token)
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

        private static IReadOnlyDictionary<string, string> ParseSourceFileMap(Dictionary<string, JToken> config)
        {
            if (config.TryGetValue("sourceFileMap", out var token)
                && token.Type == JTokenType.Object)
            {
                var json = (IEnumerable<KeyValuePair<string, JToken?>>)token;
                return json.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Value<string>() ?? string.Empty);
            }

            return ImmutableDictionary<string, string>.Empty;
        }
    }
}
