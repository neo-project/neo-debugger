using Neo;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NeoDebug.Neo3
{
    class WitnessChecker
    {
        private readonly bool bypassCheck;
        private readonly bool checkResult;
        private readonly ImmutableSortedSet<UInt160> witnesses;

        public WitnessChecker(IEnumerable<UInt160> witnesses)
        {
            bypassCheck = false;
            this.witnesses = witnesses.ToImmutableSortedSet();
        }

        public WitnessChecker(bool checkResult)
        {
            bypassCheck = true;
            this.checkResult = checkResult;
            witnesses = ImmutableSortedSet<UInt160>.Empty;
        }

        static readonly Lazy<WitnessChecker> _default = new Lazy<WitnessChecker>(() => new WitnessChecker(true));

        public static WitnessChecker Default => _default.Value;

        public bool Check(Neo.UInt160 hash)
            => bypassCheck ? checkResult : witnesses.Contains(hash);
    }
}
