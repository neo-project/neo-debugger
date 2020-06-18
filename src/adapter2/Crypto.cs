using Neo.VM;
using NeoFx;
using System;

namespace NeoDebug
{
    class Crypto : ICrypto
    {
        static readonly Lazy<ICrypto> @default = new Lazy<ICrypto>(() => new Crypto());
        public static ICrypto Default = @default.Value;

        public static byte[] Hash256(byte[] message)
        {
            var hashBuffer = new byte[HashHelpers.Hash256Size];
            if (HashHelpers.TryHash256(message, hashBuffer))
            {
                return hashBuffer;
            }

            throw new ArgumentException(nameof(message));
        }

        public static byte[] Hash160(byte[] message)
        {
            var hashBuffer = new byte[HashHelpers.Hash160Size];
            if (HashHelpers.TryHash160(message, hashBuffer))
            {
                return hashBuffer;
            }

            throw new ArgumentException(nameof(message));
        }

        public static UInt160 HashScript(byte[] script)
        {
            Span<byte> buffer = stackalloc byte[HashHelpers.Hash160Size];
            if (HashHelpers.TryHash160(script, buffer))
            {
                return new UInt160(buffer);
            }

            throw new ArgumentException(nameof(script));
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
