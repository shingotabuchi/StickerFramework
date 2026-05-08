#ifndef STICKER_NOISE_INCLUDED
#define STICKER_NOISE_INCLUDED

// Small deterministic noise helpers for URP/HLSL shaders.
// Noise ranges are approximately [0, 1] unless the function name ends with Signed.

// Hashes one scalar into one repeatable pseudo-random scalar in [0, 1].
float StickerHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

// Hashes a 2D coordinate into one repeatable pseudo-random scalar in [0, 1].
float StickerHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Hashes a 2D coordinate into one repeatable pseudo-random 2D vector in [0, 1].
float2 StickerHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// Hashes a 3D coordinate into one repeatable pseudo-random scalar in [0, 1].
float StickerHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

// Hashes a 3D coordinate into one repeatable pseudo-random 3D vector in [0, 1].
float3 StickerHash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

// Quintic interpolation curve used by value and Perlin noise.
// This is smoother than smoothstep at cell boundaries.
float2 StickerNoiseFade(float2 t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// 3D version of the quintic interpolation curve.
float3 StickerNoiseFade(float3 t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// 2D value noise.
// Hashes random scalar values at grid corners, then smoothly interpolates them.
// Cheaper than Perlin-style gradient noise, but can look blockier at low frequency.
float StickerValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = StickerNoiseFade(f);

    float a = StickerHash21(i);
    float b = StickerHash21(i + float2(1.0, 0.0));
    float c = StickerHash21(i + float2(0.0, 1.0));
    float d = StickerHash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 3D value noise.
// Same as 2D value noise, but interpolates the eight corners of a 3D grid cell.
float StickerValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = StickerNoiseFade(f);

    float n000 = StickerHash31(i + float3(0.0, 0.0, 0.0));
    float n100 = StickerHash31(i + float3(1.0, 0.0, 0.0));
    float n010 = StickerHash31(i + float3(0.0, 1.0, 0.0));
    float n110 = StickerHash31(i + float3(1.0, 1.0, 0.0));
    float n001 = StickerHash31(i + float3(0.0, 0.0, 1.0));
    float n101 = StickerHash31(i + float3(1.0, 0.0, 1.0));
    float n011 = StickerHash31(i + float3(0.0, 1.0, 1.0));
    float n111 = StickerHash31(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    float nxy0 = lerp(nx00, nx10, u.y);
    float nxy1 = lerp(nx01, nx11, u.y);

    return lerp(nxy0, nxy1, u.z);
}

// Signed 2D value noise remapped from [0, 1] to [-1, 1].
float StickerValueNoiseSigned(float2 p)
{
    return StickerValueNoise(p) * 2.0 - 1.0;
}

// Signed 3D value noise remapped from [0, 1] to [-1, 1].
float StickerValueNoiseSigned(float3 p)
{
    return StickerValueNoise(p) * 2.0 - 1.0;
}

// 2D Perlin-style gradient noise.
// This is the Perlin noise implementation: each lattice corner gets a hashed
// gradient direction, each corner contributes dot(gradient, offset), and the
// results are blended with the quintic fade curve.
float StickerGradientNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = StickerNoiseFade(f);

    float2 g00 = normalize(StickerHash22(i + float2(0.0, 0.0)) * 2.0 - 1.0);
    float2 g10 = normalize(StickerHash22(i + float2(1.0, 0.0)) * 2.0 - 1.0);
    float2 g01 = normalize(StickerHash22(i + float2(0.0, 1.0)) * 2.0 - 1.0);
    float2 g11 = normalize(StickerHash22(i + float2(1.0, 1.0)) * 2.0 - 1.0);

    float n00 = dot(g00, f - float2(0.0, 0.0));
    float n10 = dot(g10, f - float2(1.0, 0.0));
    float n01 = dot(g01, f - float2(0.0, 1.0));
    float n11 = dot(g11, f - float2(1.0, 1.0));

    return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y) * 0.5 + 0.5;
}

// Signed 2D Perlin-style gradient noise remapped from [0, 1] to [-1, 1].
float StickerGradientNoiseSigned(float2 p)
{
    return StickerGradientNoise(p) * 2.0 - 1.0;
}

// Alias for the Perlin-style 2D gradient noise.
// Use this when you specifically want "Perlin noise" at the call site.
float StickerPerlinNoise(float2 p)
{
    return StickerGradientNoise(p);
}

// Signed alias for the Perlin-style 2D gradient noise.
float StickerPerlinNoiseSigned(float2 p)
{
    return StickerGradientNoiseSigned(p);
}

// Fractal Brownian motion using value noise.
// octaves: number of noise layers.
// lacunarity: frequency multiplier per octave, commonly 2.
// gain: amplitude multiplier per octave, commonly 0.5.
float StickerFbmValue(float2 p, int octaves, float lacunarity, float gain)
{
    float sum = 0.0;
    float amplitude = 0.5;
    float normalization = 0.0;

    [loop]
    for (int i = 0; i < octaves; i++)
    {
        sum += StickerValueNoise(p) * amplitude;
        normalization += amplitude;
        p *= lacunarity;
        amplitude *= gain;
    }

    return sum / max(normalization, 0.0001);
}

// Fractal Brownian motion using Perlin-style gradient noise.
// Produces smoother, more organic layering than value-noise fBM.
float StickerFbmGradient(float2 p, int octaves, float lacunarity, float gain)
{
    float sum = 0.0;
    float amplitude = 0.5;
    float normalization = 0.0;

    [loop]
    for (int i = 0; i < octaves; i++)
    {
        sum += StickerGradientNoise(p) * amplitude;
        normalization += amplitude;
        p *= lacunarity;
        amplitude *= gain;
    }

    return sum / max(normalization, 0.0001);
}

// Convenience value-noise fBM preset: 5 octaves, lacunarity 2, gain 0.5.
float StickerFbmValue(float2 p)
{
    return StickerFbmValue(p, 5, 2.0, 0.5);
}

// Convenience Perlin-style fBM preset: 5 octaves, lacunarity 2, gain 0.5.
float StickerFbmGradient(float2 p)
{
    return StickerFbmGradient(p, 5, 2.0, 0.5);
}

// Alias for Perlin-style fBM with explicit naming at call sites.
float StickerFbmPerlin(float2 p, int octaves, float lacunarity, float gain)
{
    return StickerFbmGradient(p, octaves, lacunarity, gain);
}

// Convenience Perlin-style fBM preset: 5 octaves, lacunarity 2, gain 0.5.
float StickerFbmPerlin(float2 p)
{
    return StickerFbmGradient(p);
}

#endif
