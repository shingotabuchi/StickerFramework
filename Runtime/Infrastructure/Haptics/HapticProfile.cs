using System;
using System.Collections.Generic;
using StickerFwk.Core.Haptics;

namespace StickerFwk.Infrastructure.Haptics
{
    public class HapticProfile
    {
        private readonly HapticPattern[] _patterns;

        public HapticProfile(IEnumerable<HapticPattern> patterns)
        {
            if (patterns == null) throw new ArgumentNullException(nameof(patterns));

            var list = new List<HapticPattern>();
            foreach (var pattern in patterns)
            {
                list.Add(pattern);
            }

            _patterns = list.ToArray();
        }

        public IReadOnlyList<HapticPattern> Patterns => _patterns;
    }
}
