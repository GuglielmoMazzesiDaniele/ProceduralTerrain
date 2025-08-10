#ifndef NOISE_INCLUDED
#define NOISE_INCLUDED

// Hash function using sin
float hash(float2 p) {
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

// Generating pseudo-random 2D gradient vector
float2 gradient(float2 p) {
    float angle = hash(p) * 6.28318530718; // 2 * PI
    return float2(cos(angle), sin(angle));
}

// Fade curve as in classic Perlin
float fade(float t) {
    return t * t * t * (t * (t * 6 - 15) + 10);
}

// Optimized 2D Perlin Noise
float perlin(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);

    // Gradients at grid points
    float2 g00 = gradient(i + float2(0.0, 0.0));
    float2 g10 = gradient(i + float2(1.0, 0.0));
    float2 g01 = gradient(i + float2(0.0, 1.0));
    float2 g11 = gradient(i + float2(1.0, 1.0));

    // Distance vectors
    float2 d00 = f - float2(0.0, 0.0);
    float2 d10 = f - float2(1.0, 0.0);
    float2 d01 = f - float2(0.0, 1.0);
    float2 d11 = f - float2(1.0, 1.0);

    // Dot products
    float dot00 = dot(g00, d00);
    float dot10 = dot(g10, d10);
    float dot01 = dot(g01, d01);
    float dot11 = dot(g11, d11);

    // Fade curve
    float2 u = float2(fade(f.x), fade(f.y));

    // Bilinear interpolation
    float lerpX1 = lerp(dot00, dot10, u.x);
    float lerpX2 = lerp(dot01, dot11, u.x);
    float result = lerp(lerpX1, lerpX2, u.y);

    // Normalizing to [0,1]
    return 0.5 + 0.5 * result; 
}

float fractal_noise(float2 world_pos, int octaves, float frequency, float amplitude, float lacunarity, float gain)
{
    // Initializing
    float height = 0;
    float current_amplitude = amplitude;
    float current_frequency = frequency;

    // Iterating octaves
    for (int i = 0; i < octaves; i++)
    {
        height += perlin(world_pos * current_frequency) * current_amplitude;
        current_frequency *= lacunarity;
        current_amplitude *= gain;
    }

    // Returning the value
    return height;
}

#endif
