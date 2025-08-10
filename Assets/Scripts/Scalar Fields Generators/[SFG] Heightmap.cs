using System;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "[SFG] Heightmap", menuName = "SFGs/Heightmap")]
public class HeightmapScalarFieldGenerator : ASFG
{
    public enum HeightAlgorithm
    {
        Perlin,
        Fractal
    }

    [Flags]
    public enum TerrainFeatures
    {
        None = 0,
        Terracing = 1 << 0,
        CliffSharpening = 1 << 1,
        Carving = 1 << 2,
        Warping = 1 << 3
    }

    [Title("Base Settings")]
    [EnumToggleButtons]
    public HeightAlgorithm heightAlgorithm;

    [TitleGroup("Base Settings")]
    public float frequency;
    [TitleGroup("Base Settings")]
    public float amplitude;
    [TitleGroup("Base Settings")]
    public float bias;

    [ShowIf(nameof(heightAlgorithm), HeightAlgorithm.Fractal)]
    [TitleGroup("Base Settings/Fractal Settings")]
    public int octaves;
    [ShowIf(nameof(heightAlgorithm), HeightAlgorithm.Fractal)]
    [TitleGroup("Base Settings/Fractal Settings")]
    public float lacunarity;
    [ShowIf(nameof(heightAlgorithm), HeightAlgorithm.Fractal)]
    [TitleGroup("Base Settings/Fractal Settings")]
    public float gain;

    [Title("Advanced Features")]
    [EnumToggleButtons]
    public TerrainFeatures enabledFeatures;
    
    // Warping
    [TitleGroup("Warping")] [ShowIf("@enabledFeatures.HasFlag(TerrainFeatures.Warping)")]
    public float warpFrequency;

    [TitleGroup("Warping")] [ShowIf("@enabledFeatures.HasFlag(TerrainFeatures.Warping)")]
    public float warpStrenght;

    // Terracing
    [TitleGroup("Terracing")]
    [ShowIf("@enabledFeatures.HasFlag(TerrainFeatures.Terracing)")]
    [LabelText("Terracing Steps")]
    public int terracingSteps;

    // Cliff Sharpening
    [TitleGroup("Cliff Sharpening")]
    [ShowIf("@enabledFeatures.HasFlag(TerrainFeatures.CliffSharpening)")]
    [LabelText("Cliff Sharpness")]
    public float cliffSharpness;

    // Carving
    [TitleGroup("Carving")] [ShowIf("@enabledFeatures.HasFlag(TerrainFeatures.Carving)")]
    public float carvingFrequency;
    [TitleGroup("Carving")] [ShowIf("@enabledFeatures.HasFlag(TerrainFeatures.Carving)")]
    public float carvingWidth;
    [TitleGroup("Carving")] [ShowIf("@enabledFeatures.HasFlag(TerrainFeatures.Carving)")]
    public float carvingDepth;
    
    public override float[,,] GenerateScalarField(Vector3[,,] voxelsPositions, Vector3Int voxelsGridSize)
    {
        // Initialization
        var field = new float[voxelsGridSize.x + 1, voxelsGridSize.y + 1, voxelsGridSize.z + 1];
        
        for (var x = 0; x <= voxelsGridSize.x; x++)
        for (var y = 0; y <= voxelsGridSize.y; y++)
        for (var z = 0; z <= voxelsGridSize.z; z++)
        {
            // Computing the current position
            var position = voxelsPositions[x, y, z];
            
            // Warping
            if (enabledFeatures.HasFlag(TerrainFeatures.Warping))
            {
                // Computing a position warp
                var warp = new Vector3(
                    Mathf.PerlinNoise(position.x * warpFrequency, position.z * warpFrequency) * warpStrenght,
                    Mathf.PerlinNoise(position.z * warpFrequency, position.x * warpFrequency) * warpStrenght,
                    0.0f);
                
                // Applying the warping to the position
                position += warp;
            }
            
            // Sampling height at the position
            var height = heightAlgorithm switch
            {
                HeightAlgorithm.Fractal => Fractal(position),
                _ => Perlin(position)
            };
            
            // Cliff sharpening
            if (enabledFeatures.HasFlag(TerrainFeatures.CliffSharpening))
                height = Mathf.Pow(height, cliffSharpness);
            
            // Rivers carving
            if (enabledFeatures.HasFlag(TerrainFeatures.Carving))
            {
                // Computing the river noise
                var riverNoise = Mathf.PerlinNoise(position.x * carvingFrequency, position.z * carvingFrequency);
                
                // Computing the distance from the riverbed
                var riverDistance = Mathf.Abs(riverNoise - 0.5f) * 2f;
                
                // Affecting the height
                var riverMask = Mathf.SmoothStep(carvingWidth, 0.0f, riverDistance);
                height -= riverMask * carvingDepth;
            }
            
            // Terracing
            if (enabledFeatures.HasFlag(TerrainFeatures.Terracing))
            {
                // Mapping the height to the terracing space
                var terracingValue = height / amplitude * terracingSteps;
                
                // Flooring to the closest step
                var step = Mathf.Floor(terracingValue);
                
                // Computing the corresponding step height 
                height *= RationalSmoothstep(step, step + 1, terracingValue % 1) / terracingSteps;
            }
            
            // Caching the value
            field[x, y, z] = position.y - height - bias;
        }

        return field;
    }

    public Texture2D GenerateHeightmapFromSFG(Vector3 chunkOrigin, float chunkSize, int resolution)
    {
        // Define the voxel grid dimensions (2D slice)
        var voxelGridSize = new Vector3Int(resolution, 0, resolution);
        var voxelPositions = new Vector3[resolution + 1, 1, resolution + 1];

        // Fill voxel positions
        for (var z = 0; z <= resolution; z++)
        for (var x = 0; x <= resolution; x++)
        {
            var u = (float)x / resolution;
            var v = (float)z / resolution;
            voxelPositions[x, 0, z] = chunkOrigin + new Vector3(u * chunkSize, 0, v * chunkSize);
        }

        // Generate the scalar field using the heightmap generator
        var field = GenerateScalarField(voxelPositions, voxelGridSize);

        // Initialize the texture
        var heightmap = new Texture2D(resolution + 1, resolution + 1, TextureFormat.RFloat, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        // Transfer scalar values to texture
        for (var z = 0; z <= resolution; z++)
        for (var x = 0; x <= resolution; x++)
        {
            var height = voxelPositions[x, 0, z].y - field[x, 0, z];
            heightmap.SetPixel(x, z, new Color(height, 0, 0, 0));
        }

        heightmap.Apply();
        
        return heightmap;
    }
    
    private float Perlin(Vector3 position)
    {
        return Mathf.PerlinNoise(position.x * frequency, position.z * frequency) * amplitude;
    }
    
    private float Fractal(Vector3 position)
    {
        // Variables
        var value = 0.0f;
        var currentAmplitude = amplitude;
        var currentFrequency = frequency;
        
        // Summing up octaves
        for (var i = 0; i < octaves; i++)
        {
            // Adding current Perlin noise
            value += Mathf.PerlinNoise(position.x * currentFrequency, position.z * currentFrequency) * currentAmplitude;
            
            // Modifying the scalars for next octave
            currentFrequency *= lacunarity;
            currentAmplitude *= gain;
        }

        return value;
    }

    // Found here: https://tpfto.wordpress.com/2019/03/28/on-a-rational-variant-of-smoothstep/
    private static float RationalSmoothstep(float minimum, float maximum, float weight)
    {
        // Computing the delta
        var delta = maximum - minimum;
        
        // Mapping the weight to its actual value
        var updatedWeight = Mathf.Pow(weight, 7)
                            / (7 * Mathf.Pow(weight, 6) -
                               21 * Mathf.Pow(weight, 5) +
                               35 * Mathf.Pow(weight, 4) -
                               35 * Mathf.Pow(weight, 3) +
                               21 * Mathf.Pow(weight, 2) -
                               7 * weight +
                               1);
        
        // Returning the mapped value
        return minimum + delta * updatedWeight;
    }
}