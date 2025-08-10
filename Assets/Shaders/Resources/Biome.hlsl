#ifndef HEIGHT_INCLUDED
#define HEIGHT_INCLUDED

#include "../Resources/Auxiliary.hlsl"
#include "../Resources/Noise.hlsl"

struct fbm_parameters
{
    float frequency, amplitude;
    int octaves;
    float lacunarity, gain, bias;
    int enabled_features;
    float warp_frequency, warp_strength;
    float cliff_sharpness;
    float carving_frequency, carving_width, carving_depth;
    int terracing_steps;
};

struct height_blend {
    uint top_left;
    uint top_right;
    uint bottom_left;
    uint bottom_right;
    float2 blending;
    int blend_type;
};

StructuredBuffer<fbm_parameters> biomes_params;
StructuredBuffer<fbm_parameters> water_params;

Texture2DArray biomes_color_gradient;
StructuredBuffer<float2> biomes_height_ranges;

uint2 lut_size;

uint biome_to_biome_index(float2 biome_pos)
{
    // Flooring the position
    uint2 floored_pos = floor(biome_pos);

    // Computing the value
    return floored_pos.x + floored_pos.y * lut_size.x;
}

height_blend biome_to_biomes_blend(float2 biome_pos)
{
    // Flooring the position
    uint2 region = floor(biome_pos);
    
    // Initializing the struct
    height_blend blend;

    // Computing top left biome of the struct
    blend.top_left = (region.y >> 1) * lut_size.x + (region.x >> 1);

    // Storing the full range of biomes and initializing as full biome
    blend.top_right = blend.top_left + 1;
    blend.bottom_left = blend.top_left + lut_size.x;
    blend.bottom_right = blend.bottom_left + 1;

    // Generating the blend type
    blend.blend_type = region.x & 1 | (region.y & 1) << 1;

    // Storing the fractal part
    blend.blending = frac(biome_pos);
    
    return blend;
}

float2 world_to_biome (float2 world_pos)
{
    // Initializing the biome_pos
    float2 biomes_pos = float2(0, 0);

    // Initializing auxiliary variables
    float amplitude = 1;
    float frequency = 5e-4;

    // FBM noise
    for(int i = 0; i < 3; i++)
    {
        // Adding current octave
        biomes_pos += float2(perlin((world_pos + 500) * frequency) * amplitude,
            perlin(world_pos * frequency) * amplitude);

        // Applying octaves
        amplitude *= 0.25f;
        frequency *= 2.0f;
    }

    // Mapping into biome space
    biomes_pos *= saturate(biomes_pos) * (lut_size.y * 2 - 1);

    return biomes_pos;
}

height_blend world_to_biomes_blend (float2 world_pos)
{
    return biome_to_biomes_blend(world_to_biome(world_pos));
}

float compute_height(fbm_parameters parameters, float2 world_pos)
{
    // Initializing frequency and amplitude
    float total_height = 0;
    float current_amplitude = parameters.amplitude;
    float current_frequency = parameters.frequency;
    
    // Computing the max height, used in some filters
    float max_height = parameters.amplitude * (1.0 - pow(parameters.gain, parameters.octaves)) /
        (1.0 - parameters.gain);
    
    // Iterating octaves
    for (int i = 0; i < parameters.octaves; i++)
    {
        // --- Warping ---
        if (parameters.enabled_features & 1 << 3)
        {
            float2 warp_offset = float2(perlin(world_pos * parameters.warp_frequency),
                perlin(world_pos.yx * parameters.warp_frequency)) * parameters.warp_strength;
            world_pos = world_pos + warp_offset;
        }

        // --- Base Height ---
        float current_height = perlin(world_pos * current_frequency) * current_amplitude;

        // --- Carving ---
        if (parameters.enabled_features & 1 << 2)
        {
            float river = abs(abs(1.0f - perlin(world_pos * parameters.carving_frequency)) * 2 - 1);
            float river_distance = abs(river - 0.5) * 2.0;
            float river_mask = smoothstep(0.0, parameters.carving_width, river_distance);
            current_height = current_height - river_mask * parameters.carving_depth;
        }
    
        // Octave's effect
        current_frequency *= parameters.lacunarity;
        current_amplitude *= parameters.gain;

        // Adding the current height to the total
        total_height += current_height;
    }
    
    // --- Cliff Sharpening ---
    if (parameters.enabled_features & 1 << 1)
    {
        // Computing a ratio between the current height and the maximum height
        float ratio = total_height / max_height;

        // Rewarding cliffs
        total_height = pow(total_height, lerp(1, parameters.cliff_sharpness, ratio));
    }
    
    // --- Terracing ---
    if (parameters.enabled_features & 1 << 7)
    {
        float terracing_value = total_height / max_height * float(parameters.terracing_steps);
        float step = floor(terracing_value);
        total_height = rational_smoothstep(step, step + 1, frac(terracing_value)) /
            float(parameters.terracing_steps) * max_height;
    }

    // --- Final Bias ---
    return total_height;
}

float blend_heights(fbm_parameters first, fbm_parameters second, float weight, float2 world_pos)
{
    // Interpolating between the heights
    return tunable_smoothstep(compute_height(first, world_pos), compute_height(second, world_pos), weight, 3);
}

float compute_height(float2 world_pos)
{
    // Computing the biome position
    float2 biomes_pos = world_to_biome(world_pos);
    
    // Computing the biomes_blend
    height_blend blend = biome_to_biomes_blend(biomes_pos);

    // Blend type
    switch (blend.blend_type)
    {
        // --- PURE BIOME ---
        case 0:
        default:
            return compute_height(biomes_params[blend.top_left], world_pos);
        // --- BLENDING ON X AXIS ---
        case 1:
            return blend_heights(biomes_params[blend.top_left], biomes_params[blend.top_right],
                blend.blending.x, world_pos);
        // --- BLENDING ON Y AXIS ---
        case 2:
            return blend_heights(biomes_params[blend.top_left], biomes_params[blend.bottom_left],
                blend.blending.y, world_pos);
        // --- BILINEAR INTERPOLATION ---
        case 3:
            float top_row = blend_heights(biomes_params[blend.top_left], biomes_params[blend.top_right],
                blend.blending.x, world_pos);
            float bottom_row = blend_heights(biomes_params[blend.bottom_left], biomes_params[blend.bottom_right],
                blend.blending.x, world_pos);
        
            return tunable_smoothstep(top_row, bottom_row, blend.blending.y, 3);
    }
}

#endif
