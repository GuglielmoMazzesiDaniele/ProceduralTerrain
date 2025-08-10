#ifndef AUXILIARY_INCLUDED
#define AUXILIARY_INCLUDED

// --- MACROS ---

#define TRIPLANAR_NORMAL_SAMPLING(normalMap, sampler, blend, uvs, OUT) { \
float3 normal_xTS = UnpackNormal(normalMap.Sample(sampler, uvs[0)); \
float3 normal_yTS = UnpackNormal(normalMap.Sample(sampler, uvs[1)); \
float3 normal_zTS = UnpackNormal(normalMap.Sample(sampler, uvs[2])); \
float3 normal_xWS = normalize(float3(0.0, normal_xTS.x, normal_xTS.y)); \
float3 normal_yWS = normalize(float3(normal_yTS.x, 0.0, normal_yTS.y)); \
float3 normal_zWS = normalize(float3(normal_zTS.x, normal_zTS.y, 0.0)); \
OUT = normalize(normal_xWS * blend.x + normal_yWS * blend.y + normal_zWS * blend.z); \
}

#define TRIPLANAR_ALBEDO_SAMPLING(albedoTexture, sampler, blend, uvs, OUT) { \
OUT = float3(albedoTexture.Sample(sampler, uvs[0]).rgb * blend.x + \
albedoTexture.Sample(sampler, uvs[1]).rgb * blend.y + \
albedoTexture.Sample(sampler, uvs[2]).rgb * blend.z); \
}

// --- FUNCTIONS ---

// FUNCTIONS
float3 phong(float3 albedo, float ambient, float n_dot_l)
{
    return albedo * (ambient + (1.0 - ambient) * n_dot_l);
}

// Found here: https://tpfto.wordpress.com/2019/03/28/on-a-rational-variant-of-smoothstep/
float rational_smoothstep(float minimum, float maximum, float weight)
{
    // Computing the delta
    float delta = maximum - minimum;

    // Mapping the weight to its actual value
    float updated_weight = pow(weight, 7) /
        (7  * pow(weight, 6) -
         21 * pow(weight, 5) +
         35 * pow(weight, 4) -
         35 * pow(weight, 3) +
         21 * pow(weight, 2) -
         7 * weight +
         1);

    // Returning the mapped value
    return minimum + delta * updated_weight;
}

float3 rational_smoothstep(float3 minimum, float3 maximum, float weight)
{
    // Computing the delta
    float3 delta = maximum - minimum;

    // Mapping the weight to its actual value
    float updated_weight = pow(weight, 7) /
        (7  * pow(weight, 6) -
         21 * pow(weight, 5) +
         35 * pow(weight, 4) -
         35 * pow(weight, 3) +
         21 * pow(weight, 2) -
         7 * weight +
         1);

    // Returning the mapped value
    return minimum + delta * updated_weight;
}

// Found here: https://tpfto.wordpress.com/2019/03/28/on-a-rational-variant-of-smoothstep/
float tunable_smoothstep(float minimum, float maximum, float weight, int n)
{
    // Computing the delta
    float delta = maximum - minimum;

    // Mapping the weight to its actual value
    float updated_weight = pow(weight, n) /
        (pow(weight, n) + pow(1 - weight, n));

    // Returning the mapped value
    return minimum + delta * updated_weight;
}

// Integer hash from https://www.reedbeta.com/blog/hash-functions-for-prng/
uint wang_hash(uint x)
{
    x = x ^ 61u ^ x >> 16;
    x *= 9u;
    x = x ^ x >> 4;
    x *= 0x27d4eb2du;
    x = x ^ x >> 15;
    return x;
}

// Turns one hash into two floats in [0,1]
float2 pseudo_uv(uint seed)
{
    // Generating two random variables
    uint h1 = wang_hash(seed);
    uint h2 = wang_hash(h1);
    
    // Diving by max uint to remap into [0,1]
    const float max_reciprocal = 1.0 / 4294967295.0;

    // Computing normalized
    return float2(h1 * max_reciprocal, h2 * max_reciprocal);
}

// HLSL helper: reorders IEEE‐754 bits so uint comparison == float comparison
uint float_to_sortable_uint(float f)
{
    uint u = asuint(f);
    // if sign bit set ⇒ negative ⇒ flip all bits
    // else               ⇒ positive ⇒ flip sign bit only
    return (u & 0x80000000u) != 0
        ? ~u
        : (u ^ 0x80000000u);
}

#endif