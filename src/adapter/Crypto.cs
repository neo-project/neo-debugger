using Neo.VM;
using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace NeoDebug.Adapter
{
    class Crypto : ICrypto
    {
        public static readonly Lazy<SHA256> SHA256 = new Lazy<SHA256>(() => System.Security.Cryptography.SHA256.Create());

        // TODO: replace SHA1 with RIPEMD-160 implementaiton
        private static readonly Lazy<SHA1> SHA1 = new Lazy<SHA1>(() => System.Security.Cryptography.SHA1.Create());

        public static byte[] Hash256(byte[] message)
        {
            var hashBuffer = new byte[32];
            if (NeoFx.Utility.TryHash256(message, hashBuffer))
            {
                return hashBuffer;
            }

            throw new Exception();
        }

        public static byte[] Hash160(byte[] message)
        {
            var hashBuffer = new byte[20];
            if (NeoFx.Utility.TryHash160(message, hashBuffer))
            {
                return hashBuffer;
            }

            throw new Exception();
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
