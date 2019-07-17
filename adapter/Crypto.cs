using Neo.VM;
using System;
using System.Security.Cryptography;

namespace Neo.DebugAdapter
{
    class Crypto : ICrypto
    {
        static readonly Lazy<SHA256> sha256 = new Lazy<SHA256>(() => SHA256.Create());
        static readonly Lazy<SHA1> sha1 = new Lazy<SHA1>(() => SHA1.Create());

        public static byte[] Hash256(byte[] message)
        {
            var hash1 = sha256.Value.ComputeHash(message);
            return sha256.Value.ComputeHash(hash1);
        }

        public static byte[] Hash160(byte[] message)
        {
            var hash1 = sha256.Value.ComputeHash(message);
            return sha1.Value.ComputeHash(hash1);
        }

        // Note, byte arrays have reference semantics for GetHashCode
        // GetHashCode<T> provides a value semantic hash code for a Span of T's
        public static int GetHashCode<T>(ReadOnlySpan<T> span)
        {
            int hash = default(T).GetHashCode();
            for (int i = 0; i < span.Length; i++)
            {
                hash = HashCode.Combine(hash, i, span[i]);
            }
            return hash;
        }

        public static int GetHashCode<T>(T[] array)
        {
            return GetHashCode<T>(array.AsSpan());
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
