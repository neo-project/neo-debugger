using System;
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
using Neo.BlockchainToolkit.SmartContract;
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

            var traceFile = jsonInvocation.Type == JTokenType.Object
                ? jsonInvocation.Value<string>("trace-file") : null;
            if (traceFile != null)
            {
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
            var launchManifest = await LoadContractManifestAsync(program).ConfigureAwait(false);
            var chain = LoadNeoExpress(config);
            var invocation = ParseInvocation(jsonInvocation);

            var checkpoint = LoadBlockchainCheckpoint(config, chain?.Network, chain?.AddressVersion);

            var (trigger, witnessChecker) = ParseRuntime(config, chain, checkpoint.Settings.AddressVersion);
            if (trigger != TriggerType.Application)
            {
                throw new Exception($"Trigger Type {trigger} not supported");
            }

            var store = new MemoryTrackingStore(checkpoint);
            store.EnsureLedgerInitialized(checkpoint.Settings);

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = Array.Empty<byte>(),
                Signers = GetSigners(config, chain, checkpoint.Settings),
                ValidUntilBlock = checkpoint.Settings.MaxValidUntilBlockIncrement,
                Attributes = Array.Empty<TransactionAttribute>(),                
                Witnesses = Array.Empty<Witness>()
            };

            var launchContractHash = UInt160.Zero;

            if (invocation.IsT3) // deployment invocation
            {
                if (tx.Signers.Length == 0)
                {
                    tx.Signers = TryGetDeploymentSigner(config, chain, checkpoint.Settings, out var deploymentSigner)
                        ? new[] { deploymentSigner }
                        : new[] { new Signer { Account = UInt160.Zero } };
                }

                launchContractHash = Neo.SmartContract.Helper.GetContractHash(tx.Sender, launchNefFile.CheckSum, launchManifest.Name);

                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(NativeContract.ContractManagement.Hash, "deploy", launchNefFile.ToArray(), launchManifest.ToJson().ToString());
                tx.Script = builder.ToArray();
            }
            else
            {
                if (tx.Signers.Length == 0)
                {
                    tx.Signers = new[] { new Signer { Account = UInt160.Zero } };
                }

                var deploymentSigner = TryGetDeploymentSigner(config, chain, checkpoint.Settings, out var _deploymentSigner)
                    ? _deploymentSigner : new Signer { Account = UInt160.Zero };
                launchContractHash = EnsureContractDeployed(store, launchNefFile, launchManifest, deploymentSigner, checkpoint.Settings);

                var paramParser = CreateContractParameterParser(checkpoint.Settings.AddressVersion, store, chain);
                UpdateContractStorage(store, launchContractHash, ParseStorage(config, paramParser));
                tx.Script = await CreateInvokeScriptAsync(invocation, program, launchContractHash, paramParser);

                if (invocation.TryPickT1(out var oracleResponse, out _))
                {
                    tx.Attributes = GetTransactionAttributes(oracleResponse, store, launchContractHash, paramParser);
                }
            }

            if (tx.Script.Length == 0) throw new InvalidOperationException("Debug transaction script length zero");
            if (tx.Signers.Length == 0) throw new InvalidOperationException("Debug transaction signers length zero");
            if (launchContractHash == UInt160.Zero) throw new InvalidOperationException("Debug contract hash could not determined");

            // TODO: load other contracts
            //          Not sure supporting other contracts is a good idea anymore. Since there's no way to calculate the 
            //          contract id hash prior to deployment in Neo 3, I'm thinking the better approach would be to simply
            //          deploy whatever contracts you want and take a snapshot rather than deploying multiple contracts 
            //          during launch configuration.

            var block = CreateDummyBlock(store, tx);
            var engine = new DebugApplicationEngine(tx, store, checkpoint.Settings, block, witnessChecker);
            engine.LoadScript(tx.Script);
            return engine;
        }

        static NefFile LoadNefFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, false);
            return reader.ReadSerializable<NefFile>();
        }

        static async Task<ContractManifest> LoadContractManifestAsync(string contractPath)
        {
            var bytesManifest = await File.ReadAllBytesAsync(
                Path.ChangeExtension(contractPath, ".manifest.json")).ConfigureAwait(false);
            return ContractManifest.Parse(bytesManifest);
        }

        static ExpressChain? LoadNeoExpress(ConfigProps config)
        {
            if (config.TryGetValue("neo-express", out var neoExpressPath))
            {
                var fs = new System.IO.Abstractions.FileSystem();
                return fs.LoadChain(neoExpressPath.Value<string>()
                    ?? throw new JsonException("invalid string"));
            }
            else
            {
                return null;
            }
        }

        static Invocation ParseInvocation(JToken jsonInvocation)
        {
            return InvokeFileInvocation.TryFromJson(jsonInvocation, out var invokeFileInvocation)
                ? invokeFileInvocation
                : OracleResponseInvocation.TryFromJson(jsonInvocation, out var oracleInvocation)
                    ? oracleInvocation
                    : LaunchInvocation.TryFromJson(jsonInvocation, out var launchInvocation)
                        ? launchInvocation
                        : ContractDeployInvocation.TryFromJson(jsonInvocation, out var deployInvocation)
                            ? deployInvocation
                            : throw new JsonException("invalid invocation property");
        }

        static ICheckpointStore LoadBlockchainCheckpoint(ConfigProps config, uint? network = null, byte? addressVersion = null)
        {
            if (config.TryGetValue("checkpoint", out var checkpoint))
            {
                var checkpointPath = checkpoint.Value<string>() ?? throw new JsonException();
                return new CheckpointStore(checkpointPath, network, addressVersion);
            }
            else
            {
                return new NullCheckpointStore(network, addressVersion);
            }
        }

        static ContractParameterParser CreateContractParameterParser(byte addressVersion, IStore store, ExpressChain? chain)
        {
            ContractParameterParser.TryGetUInt160 tryGetContract = (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) =>
                {
                    using var snapshot = new SnapshotCache(store);
                    foreach (var contract in NativeContract.ContractManagement.ListContracts(snapshot))
                    {
                        if (contract.Manifest.Name.Equals(name))
                        {
                            scriptHash = contract.Hash;
                            return true;
                        }
                    }

                    scriptHash = null!;
                    return false;
                };

            ContractParameterParser.TryGetUInt160? tryGetAccount = chain == null
                ? null
                : (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) =>
                {
                    if (chain.TryGetDefaultAccount(name, out var account))
                    {
                        scriptHash = account.ScriptHash.ToScriptHash(addressVersion);
                        return true;
                    }

                    scriptHash = null!;
                    return false;
                };

            return new ContractParameterParser(addressVersion, tryGetAccount, tryGetContract);
        }

        static UInt160 EnsureContractDeployed(IStore store, NefFile nefFile, ContractManifest manifest, Signer deploySigner, ProtocolSettings settings)
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

                        return contract.Hash;
                    }
                }
            }

            // if no existing contract with matching manifest.Name is found, deploy the contract
            using (var snapshot = new SnapshotCache(store.GetSnapshot()))
            {
                var contractState = Deploy(snapshot, deploySigner, nefFile, manifest, settings);
                snapshot.Commit();

                return contractState.Hash;
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
                    var tx = TestApplicationEngine.CreateTestTransaction(deploySigner);
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

        static void UpdateContractStorage(IStore store, UInt160 contractHash, Storages storages)
        {
            using var snapshot = new SnapshotCache(store.GetSnapshot());
            var contract = NativeContract.ContractManagement.GetContract(snapshot, contractHash);
            if (contract is null) throw new ArgumentException($"{contractHash} contract not found", nameof(contractHash));

            foreach (var (key, storageItem) in storages)
            {
                var storageKey = new StorageKey() { Id = contract.Id, Key = key };
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

        static Block CreateDummyBlock(IStore store, Transaction? tx = null)
        {
            using var snapshot = new SnapshotCache(store);
            var block = TestApplicationEngine.CreateDummyBlock(snapshot, ProtocolSettings.Default);
            if (tx != null) block.Transactions = new[] { tx };
            return block;
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

                    if (Neo.Utility.StrictUTF8.GetByteCount(invocation.Url) > MaxUrlLength) throw new ArgumentException("Invalid URL Length");
                    if (Neo.Utility.StrictUTF8.GetByteCount(invocation.Filter) > MaxFilterLength) throw new ArgumentException("Invalid Filter Length");
                    if (Neo.Utility.StrictUTF8.GetByteCount(invocation.Callback) > MaxCallbackLength) throw new ArgumentException("Invalid Callback Length");
                    if (invocation.Callback.StartsWith('_')) throw new ArgumentException($"Invalid Callback {invocation.Callback}");

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
                    _ => throw new ArgumentException($"Invalid Witness length {hashOrPubkey.Length}")
                };
                return witnesses.Contains(hash);
            }
        }

        static Signer[] GetSigners(ConfigProps config, ExpressChain? chain, ProtocolSettings settings)
            => GetSigners(config, chain, settings.AddressVersion);

        static Signer[] GetSigners(ConfigProps config, ExpressChain? chain, byte version)
        {
            if (config.TryGetValue("signers", out var signersJson)
                && signersJson is JArray signersArray
                && signersArray.Count > 0)
            {
                return signersArray.Select(j => ParseSigner(j, chain, version)).ToArray();
            }

            return Array.Empty<Signer>();
        }

        static bool TryGetDeploymentSigner(ConfigProps config, ExpressChain? chain, ProtocolSettings settings, [MaybeNullWhen(false)] out Signer signer)
            => TryGetDeploymentSigner(config, chain, settings.AddressVersion, out signer);

        static bool TryGetDeploymentSigner(ConfigProps config, ExpressChain? chain, byte version, [MaybeNullWhen(false)] out Signer signer)
        {
            if (config.TryGetValue("deploy-signer", out var deploySignerToken))
            {
                signer = ParseSigner(deploySignerToken, chain, version);
                return true;
            }

            signer = default;
            return false;
        }

        static Signer ParseSigner(JToken token, ExpressChain? chain, byte version)
        {
            if (token.Type == JTokenType.String)
            {
                var address = ParseAddress(token.Value<string>(), chain, version);
                return new Signer { Account = address, Scopes = WitnessScope.CalledByEntry };
            }
            else if (token.Type == JTokenType.Object)
            {
                var address = ParseAddress(token.Value<string>("account"), chain, version);
                var scopes = ParseScopes(token.Value<string>("scopes"));
                var allowedContracts = token["allowedcontracts"]?.Select(j => UInt160.Parse(j.Value<string>())).ToArray();
                var allowedGropus = token["allowedgroups"]?.Select(j => ECPoint.Parse(j.Value<string>(), ECCurve.Secp256r1)).ToArray();

                return new Signer
                {
                    Account = address,
                    Scopes = scopes,
                    AllowedContracts = allowedContracts,
                    AllowedGroups = allowedGropus,
                };
            }
            else
            {
                throw new JsonException("Invalid signer JSON");
            }

            static WitnessScope ParseScopes(string? text) 
                => text is null ? WitnessScope.CalledByEntry : Enum.Parse<WitnessScope>(text);
        }

        static UInt160 ParseAddress(string? text, ExpressChain? chain, byte version)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("null or empty address string", nameof(text));

            if (text[0] == '@' && chain is not null)
            {
                var accountName = text[1..];

                if (string.Equals(accountName, "genesis", StringComparison.OrdinalIgnoreCase))
                {
                    return chain.CreateGenesisContract().ScriptHash;
                }

                if (chain.Wallets != null && chain.Wallets.Count > 0)
                {
                    for (int i = 0; i < chain.Wallets.Count; i++)
                    {
                        if (string.Equals(accountName, chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return ToScriptHash(chain.Wallets[i], version);
                        }
                    }
                }

                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    if (string.Equals(accountName, chain.ConsensusNodes[i].Wallet.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return ToScriptHash(chain.ConsensusNodes[i].Wallet, version);
                    }
                }
            }

            try { return text.ToScriptHash(version); }
            catch { /* ignore ToScriptHash exception */ }

            if (UInt160.TryParse(text, out var uInt160)) return uInt160;

            throw new ArgumentException($"invalid address string {text}", nameof(text));

            static UInt160 ToScriptHash(ExpressWallet wallet, byte version)
            {
                var account = wallet.DefaultAccount ?? throw new InvalidOperationException($"{wallet.Name} wallet missing default account");
                return account.ScriptHash.ToScriptHash(version);
            }
        }

        static async IAsyncEnumerable<DebugInfo> LoadDebugInfosAsync(ConfigProps config, IReadOnlyDictionary<string, string> sourceFileMap)
        {
            var program = ParseProgram(config);
            var debugInfo = (await DebugInfo.LoadAsync(program, sourceFileMap).ConfigureAwait(false))
                .Match(di => di, _ => throw new FileNotFoundException(program));
            yield return debugInfo;

            if (config.TryGetValue("stored-contracts", out var storedContracts))
            {
                foreach (var storedContract in storedContracts)
                {
                    if (storedContract.Type == JTokenType.String)
                    {
                        program = storedContract.Value<string>() ?? throw new JsonException("invalid stored-contracts item");
                        debugInfo = (await DebugInfo.LoadAsync(program, sourceFileMap).ConfigureAwait(false))
                            .Match(di => di, _ => throw new FileNotFoundException(program));
                        yield return debugInfo;
                    }
                    else if (storedContract.Type == JTokenType.Object)
                    {
                        program = storedContract.Value<string>("program") ?? throw new JsonException("missing program property");
                        debugInfo = (await DebugInfo.LoadAsync(program, sourceFileMap).ConfigureAwait(false))
                            .Match(di => di, _ => throw new FileNotFoundException(program));
                        yield return debugInfo;
                    }
                    else
                    {
                        throw new Exception("invalid stored-contract value");
                    }
                }
            }
        }
    }
}
