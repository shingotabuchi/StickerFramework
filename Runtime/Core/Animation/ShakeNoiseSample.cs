namespace StickerFwk.Core
{
    /// <summary>
    /// Three-axis shake offset sample in normalized -1..1 space.
    /// </summary>
    public readonly struct ShakeNoiseSample
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public ShakeNoiseSample(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
