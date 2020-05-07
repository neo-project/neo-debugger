using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoDebug
{
    class WitnessChecker
    {
        private readonly bool bypassCheck;
        private readonly bool checkResult;
        private readonly IEnumerable<byte[]> witnesses;

        public WitnessChecker(IEnumerable<byte[]> witnesses)
        {
            bypassCheck = false;
            this.witnesses = witnesses;
        }

        public WitnessChecker(bool checkResult)
        {
            bypassCheck = true;
            this.checkResult = checkResult;
            witnesses = Enumerable.Empty<byte[]>();
        }

        static readonly Lazy<WitnessChecker> _default = new Lazy<WitnessChecker>(() => new WitnessChecker(true));

        public static WitnessChecker Default => _default.Value;

        public bool Check(byte[] hash)
        {
            if (bypassCheck)
            {
                return checkResult;
            }
            else
            {
                var hashSpan = hash.AsSpan();
                foreach (var witness in witnesses)
                {
                    if (hashSpan.SequenceEqual(witness))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
