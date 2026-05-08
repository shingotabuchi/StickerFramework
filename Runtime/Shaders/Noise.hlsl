#ifndef STICKER_NOISE_INCLUDED
#define STICKER_NOISE_INCLUDED

// Small deterministic noise helpers for URP/HLSL shaders.
// Ranges are approximately [0, 1] unless the function name ends with Signed.

float StickerHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float StickerHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 StickerHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float StickerHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float3 StickerHash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float2 StickerNoiseFade(float2 t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

float3 StickerNoiseFade(float3 t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

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

float StickerValueNoiseSigned(float2 p)
{
    return StickerValueNoise(p) * 2.0 - 1.0;
}

float StickerValueNoiseSigned(float3 p)
{
    return StickerValueNoise(p) * 2.0 - 1.0;
}

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

float StickerGradientNoiseSigned(float2 p)
{
    return StickerGradientNoise(p) * 2.0 - 1.0;
}

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

float StickerFbmValue(float2 p)
{
    return StickerFbmValue(p, 5, 2.0, 0.5);
}

float StickerFbmGradient(float2 p)
{
    return StickerFbmGradient(p, 5, 2.0, 0.5);
}

#endif
