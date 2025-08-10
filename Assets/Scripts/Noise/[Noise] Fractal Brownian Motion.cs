using System;
using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using UnityEngine;
using Unity.Mathematics;

public class FractalBrownianMotion
{
    [Flags]
    public enum HeightmapFeatures
    {
        None = 0,
        CliffSharpening = 1 << 1,
        Carving = 1 << 2,
        Warping = 1 << 3,
        Terracing = 1 << 7,
    }
    
    [Serializable] [StructLayout(LayoutKind.Sequential)]
    public struct Parameters
    {
        [Title("Fractal Noise - Base Settings")] 
        public float frequency;
        public float amplitude;
        public int octaves;
        public float lacunarity;
        public float gain;
        public float bias;
        
        [Title("Fractal Noise - Advanced Settings")] [EnumToggleButtons]
        public HeightmapFeatures enabledFeatures;
        
        // Warping
        [TitleGroup("Warping")] [ShowIf("@enabledFeatures.HasFlag(HeightmapFeatures.Warping)")]
        public float warpFrequency;
        [TitleGroup("Warping")] [ShowIf("@enabledFeatures.HasFlag(HeightmapFeatures.Warping)")]
        public float warpStrength;
        
        // Cliff Sharpening
        [TitleGroup("Cliff Sharpening")]
        [ShowIf("@enabledFeatures.HasFlag(HeightmapFeatures.CliffSharpening)")]
        [LabelText("Cliff Sharpness")]
        public float cliffSharpness;
        
        // Carving
        [TitleGroup("Carving")] [ShowIf("@enabledFeatures.HasFlag(HeightmapFeatures.Carving)")]
        public float carvingFrequency;

        [TitleGroup("Carving")] [ShowIf("@enabledFeatures.HasFlag(HeightmapFeatures.Carving)")]
        public float carvingWidth;

        [TitleGroup("Carving")] [ShowIf("@enabledFeatures.HasFlag(HeightmapFeatures.Carving)")]
        public float carvingDepth;
        
        // Terracing
        [TitleGroup("Terracing")]
        [ShowIf("@enabledFeatures.HasFlag(HeightmapFeatures.Terracing)")]
        [LabelText("Terracing Steps")]
        public int terracingSteps;
    }

    public static float FBM (Parameters parameters, float2 position)
    {
        // Initializing variables
        var currentFrequency = parameters.frequency;
        var currentAmplitude = parameters.amplitude;
        var totalNoise = 0.0f;
        
        // Computing the max height, used in some filters
        var maxNoise = parameters.amplitude * (1.0f - math.pow(parameters.gain, parameters.octaves)) /
                           (1.0f - parameters.gain);
        
        // Iterating octaves
        for (var i = 0; i < parameters.octaves; i++)
        {
            // --- Warping ---
            if ((parameters.enabledFeatures & HeightmapFeatures.Warping) != 0)
            {
                // Computing the warp offset
                var offset = new float2(noise.cnoise(position * parameters.warpFrequency), 
                    noise.cnoise(position.yx * parameters.warpFrequency)) * parameters.warpStrength;
                
                // Applying the offset
                position += offset;
            }
            
            // Current octave noise
            var currentNoise = noise.snoise(position * currentFrequency) * currentAmplitude;
            
            // --- Carving ---
            if ((parameters.enabledFeatures & HeightmapFeatures.Carving) != 0)
            {
                // Computing the river value
                var river = math.abs((1.0f - noise.cnoise(position * parameters.carvingFrequency)) * 2 - 1);
                
                // Computing the distance to the river
                var riverDistance = math.abs(river - 0.5f) * 2.0f;

                // Creating the mask
                var weight = math.smoothstep(0.0f, parameters.carvingWidth, riverDistance);
                var riverMask = math.lerp(0.0f, parameters.carvingWidth, weight);
                
                // Applying the carving
                currentNoise -= riverMask * parameters.carvingDepth;
            }

            // Octave's effect
            currentFrequency *= parameters.lacunarity;
            currentAmplitude *= parameters.gain;
            
            // Adding the current noise to the total
            totalNoise += currentNoise;
        }
        
        // --- Cliff Sharpening ---
        if ((parameters.enabledFeatures & HeightmapFeatures.CliffSharpening) != 0)
        {
            // Computing a ratio between the current height and the maximum height
            var ratio = totalNoise / maxNoise;

            // Rewarding cliffs
            totalNoise = math.pow(totalNoise, math.lerp(1.0f, parameters.cliffSharpness, ratio));
        }
    
        // --- Terracing ---
        if ((parameters.enabledFeatures & HeightmapFeatures.Terracing) != 0)
        {
            var terracing_value = totalNoise / maxNoise * parameters.terracingSteps;
            var step = math.floor(terracing_value);
            totalNoise = CustomMath.TunableSmoothstep(step, step + 1, terracing_value % 1, 7) /
                parameters.terracingSteps * maxNoise;
        }

        return totalNoise + parameters.bias;
    }

    public static Vector2 ComputeHeightBounds(Parameters parameters)
    {
        // Applying each octave contribution
        var minHeight = 0f;
        var maxHeight = 0f;
        var amplitude  = parameters.amplitude;
        for (var i = 0; i < parameters.octaves; i++)
        {
            // Each octave contributes between 0 and amplitude
            maxHeight += Mathf.Max(amplitude, 0f);
            minHeight += Mathf.Min(amplitude, 0f);
            
            amplitude *= parameters.gain;
        }

        // Carving
        if (parameters.enabledFeatures.HasFlag(HeightmapFeatures.Carving)) 
            minHeight -= parameters.carvingDepth;
        
        // Returning
        return new Vector2(minHeight + parameters.bias, maxHeight + parameters.bias);
    }
}
