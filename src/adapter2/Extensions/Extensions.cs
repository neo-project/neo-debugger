using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace NeoDebug
{
    public static class Extensions
    {
        //https://stackoverflow.com/a/1646913
        public static int GetSequenceHashCode(this ReadOnlySpan<byte> span)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < span.Length; i++)
                {
                    hash = hash * 31 + span[i];
                }
                return hash;
            }
        }

        public static int GetSequenceHashCode(this byte[] array)
        {
            return GetSequenceHashCode(array.AsSpan());
        }

        public static IEnumerable<(string name, string type)> GetLocals(this DebugInfo.Method method)
            => method.Parameters.Concat(method.Variables);

        public static string ToHexString(this BigInteger bigInteger)
            => "0x" + bigInteger.ToString("x");

        public static string ToHexString(this ReadOnlySpan<byte> span)
            => ToHexString(new BigInteger(span));

        public static string ToHexString(this byte[] array)
            => ToHexString(array.AsSpan());

        public static bool TryParseBigInteger(this string value, out BigInteger bigInteger)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && BigInteger.TryParse(value.AsSpan().Slice(2), NumberStyles.HexNumber, null, out bigInteger))
            {
                return true;
            }

            Lazy<SHA256> sha256 = new Lazy<SHA256>(() => SHA256.Create());

            if (value.StartsWith("@", StringComparison.Ordinal))
            {
                Span<byte> tempBuffer = stackalloc byte[32];
                Span<byte> checksum = stackalloc byte[32];
                var decoded = SimpleBase.Base58.Bitcoin.Decode(value.AsSpan().Slice(1));

                if (decoded.Length == 25 // address version byte + 20 bytes address + 4 byte checksum
                    && decoded[0] == 23  // Address version 23 used by mainnet, testnet and Neo Express
                    && sha256.Value.TryComputeHash(decoded.Slice(0, 21), tempBuffer, out var written1)
                    && sha256.Value.TryComputeHash(tempBuffer, checksum, out var written2)
                    && written1 == 32 && written2 == 32
                    && decoded.Slice(21).SequenceEqual(checksum.Slice(0, 4)))
                {
                    bigInteger = new BigInteger(decoded.Slice(1, 20));
                    return true;
                }
            }

            bigInteger = default;
            return false;
        }
    }
}
