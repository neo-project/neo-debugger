using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.VM;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Neo.DebugAdapter
{
    internal class Crypto : ICrypto
    {
        static Lazy<SHA256> sha256 = new Lazy<SHA256>(() => SHA256.Create());

        public static byte[] Hash256(byte[] message)
        {
            var hash1 = sha256.Value.ComputeHash(message);
            return sha256.Value.ComputeHash(hash1);
        }

        static Lazy<SHA1> sha1 = new Lazy<SHA1>(() => SHA1.Create());

        public static byte[] Hash160(byte[] message)
        {
            var hash1 = sha256.Value.ComputeHash(message);
            return sha1.Value.ComputeHash(hash1);
        }

        byte[] ICrypto.Hash160(byte[] message)
        {
            return Hash160(message);
        }

        byte[] ICrypto.Hash256(byte[] message)
        {
            return Hash256(message);
        }

        bool ICrypto.VerifySignature(byte[] message, byte[] signature, byte[] pubkey)
        {
            throw new NotImplementedException();
        }
    }
}
