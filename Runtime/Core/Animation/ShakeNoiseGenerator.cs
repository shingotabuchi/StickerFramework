using System;

namespace StickerFwk.Core
{
    /// <summary>
    /// Pure C# generator for normalized shake noise samples.
    /// </summary>
    public sealed class ShakeNoiseGenerator
    {
        private const int HashMask = 255;

        private readonly int _seed;
        private readonly int[] _permutation;

        public ShakeNoiseGenerator(int seed)
        {
            _seed = seed;
            _permutation = BuildPermutation(seed);
        }

        public ShakeNoiseSample Sample(ShakeNoiseType noiseType, float time)
        {
            return noiseType switch
            {
                ShakeNoiseType.Random => RandomSample(time),
                ShakeNoiseType.Perlin => PerlinSample(time),
                _ => PerlinSample(time)
            };
        }

        private ShakeNoiseSample RandomSample(float time)
        {
            var sampleIndex = FastFloor(time);
            return new ShakeNoiseSample(
                HashSignedValue(sampleIndex, 0),
                HashSignedValue(sampleIndex, 1),
                HashSignedValue(sampleIndex, 2));
        }

        private float HashSignedValue(int sampleIndex, int axis)
        {
            unchecked
            {
                var value = _seed;
                value = (value * 397) ^ sampleIndex;
                value = (value * 397) ^ axis;
                value ^= value >> 16;
                value *= unchecked((int)0x7feb352d);
                value ^= value >> 15;
                value *= unchecked((int)0x846ca68b);
                value ^= value >> 16;

                var normalized = (uint)value / (float)uint.MaxValue;
                return normalized * 2f - 1f;
            }
        }

        private ShakeNoiseSample PerlinSample(float time)
        {
            return new ShakeNoiseSample(
                Perlin1D(time),
                Perlin1D(time + 37.37f),
                Perlin1D(time + 71.71f));
        }

        private float Perlin1D(float x)
        {
            var floor = FastFloor(x);
            var local = x - floor;
            var left = floor & HashMask;
            var right = (left + 1) & HashMask;
            var fade = Fade(local);

            var a = Gradient(_permutation[left], local);
            var b = Gradient(_permutation[right], local - 1f);
            return Lerp(a, b, fade) * 2f;
        }

        private static int[] BuildPermutation(int seed)
        {
            var source = new int[HashMask + 1];
            for (var i = 0; i < source.Length; i++)
            {
                source[i] = i;
            }

            var random = new Random(seed);
            for (var i = source.Length - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                (source[i], source[swapIndex]) = (source[swapIndex], source[i]);
            }

            return source;
        }

        private static int FastFloor(float value)
        {
            var integer = (int)value;
            return value < integer ? integer - 1 : integer;
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float Gradient(int hash, float x)
        {
            return (hash & 1) == 0 ? x : -x;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
