using System;
using System.Diagnostics.CodeAnalysis;
using Neo;
using Neo.BlockchainToolkit;

namespace NeoDebug.Neo3
{
    public static class Utility
    {
        public static int DigitCount(int n)
        {
            if (n < 10) return 1;
            if (n < 100) return 2;
            if (n < 1000) return 3;
            if (n < 10000) return 4;
            if (n < 100000) return 5;
            if (n < 1000000) return 6;
            if (n < 10000000) return 7;
            if (n < 100000000) return 8;
            if (n < 1000000000) return 9;
            return 10;
        }

        public static UInt160 ParseContractHash(this ContractParameterParser paramParser, string contract)
            => TryParseContractHash(paramParser, contract, out var hash)
                ? hash
                : throw new ArgumentException($"Failed to parse \"{contract}\"", nameof(contract));

        public static bool TryParseContractHash(this ContractParameterParser paramParser, string contract, [MaybeNullWhen(false)] out UInt160 hash)
        {
            if (paramParser.TryLoadScriptHash(contract, out hash)) return true;
            if (UInt160.TryParse(contract, out hash)) return true;
            return false;
        }
    }
}
