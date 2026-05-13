using NUnit.Framework;
using StickerFwk.Core;
using NUnitAssert = NUnit.Framework.Assert;

namespace StickerFwk.Tests.Runtime
{
    public class ShakeNoiseGeneratorTests
    {
        [Test]
        public void PerlinSamplesAreDeterministicForSeedAndTime()
        {
            var first = new ShakeNoiseGenerator(123);
            var second = new ShakeNoiseGenerator(123);

            var firstSample = first.Sample(ShakeNoiseType.Perlin, 0.25f);
            var secondSample = second.Sample(ShakeNoiseType.Perlin, 0.25f);

            NUnitAssert.That(secondSample.X, Is.EqualTo(firstSample.X));
            NUnitAssert.That(secondSample.Y, Is.EqualTo(firstSample.Y));
            NUnitAssert.That(secondSample.Z, Is.EqualTo(firstSample.Z));
        }

        [Test]
        public void SamplesStayNormalized()
        {
            var generator = new ShakeNoiseGenerator(123);

            for (var i = 0; i < 100; i++)
            {
                var randomSample = generator.Sample(ShakeNoiseType.Random, i);
                AssertNormalized(randomSample);

                var perlinSample = generator.Sample(ShakeNoiseType.Perlin, i * 0.1f);
                AssertNormalized(perlinSample);
            }
        }

        [Test]
        public void RandomSamplesChangeByWholeSampleTime()
        {
            var generator = new ShakeNoiseGenerator(123);

            var first = generator.Sample(ShakeNoiseType.Random, 3.1f);
            var sameBucket = generator.Sample(ShakeNoiseType.Random, 3.9f);
            var nextBucket = generator.Sample(ShakeNoiseType.Random, 4f);

            NUnitAssert.That(sameBucket.X, Is.EqualTo(first.X));
            NUnitAssert.That(sameBucket.Y, Is.EqualTo(first.Y));
            NUnitAssert.That(sameBucket.Z, Is.EqualTo(first.Z));
            var changed = nextBucket.X != first.X || nextBucket.Y != first.Y || nextBucket.Z != first.Z;
            NUnitAssert.That(changed, Is.True);
        }

        private static void AssertNormalized(ShakeNoiseSample sample)
        {
            NUnitAssert.That(sample.X, Is.InRange(-1f, 1f));
            NUnitAssert.That(sample.Y, Is.InRange(-1f, 1f));
            NUnitAssert.That(sample.Z, Is.InRange(-1f, 1f));
        }
    }
}
