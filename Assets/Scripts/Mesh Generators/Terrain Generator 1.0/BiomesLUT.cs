using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

[CreateAssetMenu(fileName = "Biomes LUT", menuName = "Biomes/Biomes LUT")]
public class BiomesLUT : SerializedScriptableObject
{
    [Title("LUT - UI")]
    public Vector2Int lookUpTableSize;
    [TableMatrix(
        SquareCells = true,
        HorizontalTitle = "Temperature ->",
        VerticalTitle = "Moisture ->")
    ]
    [OdinSerialize]
    public Biome[,] LUT = new Biome[2, 2];

    [Title("LUT - Texture")] 
    public Vector2Int lookUpTableResolution;
    
    private void OnValidate()
    {
        // Creating a new LUT with the new resolution
        var newLUT = new Biome [lookUpTableSize.x, lookUpTableSize.y];
        
        // Copying previous value into the new LUT
        for (var y = 0; y < LUT.GetLength(1) && y < lookUpTableSize.y; y++)
        {
            for (var x = 0; x < LUT.GetLength(0) && x < lookUpTableSize.x; x++)
            {
                // Copying the biome reference into the new LUT
                newLUT[x, y] = LUT[x, y];
            }
        }
        
        // Setting the new LUT
        LUT = newLUT;
    }

    public (Texture2D, Texture2D) GetLUT()
    {
        // Initializing auxiliary variables
        var ExpandedLUTWidth = lookUpTableSize.x * 2 - 1;
        var ExpandedLUTHeight = lookUpTableSize.y * 2 - 1;
        var textureWidth = lookUpTableResolution.x;
        var textureHeight = lookUpTableResolution.y;
        
        // Create an RGBA float texture—no mip, linear (we'll use point‐sampling)
        var biomesIndices = new Texture2D(lookUpTableResolution.x, lookUpTableResolution.y, 
            TextureFormat.RGBAFloat, false, true)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        
        var blendingWeights = new Texture2D(lookUpTableResolution.x, lookUpTableResolution.y, 
            TextureFormat.RGFloat, false, true)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        
        // Iterating the biomes
        for (var y = 0; y < textureHeight; y++)
        for (var x = 0; x < textureWidth; x++)
        {
            // Mapping from texel space to LUT space
            var position = new Vector2((x + 0.5f) / textureWidth * ExpandedLUTWidth,
                (y + 0.5f) / textureHeight * ExpandedLUTHeight);
            
            // Regions
            var region = new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
            
            // Regions' fractals
            var fractal = new Vector2(position.x - region.x, position.y - region.y);
            
            // Initializing pixel color
            var indices = new Color();
            var weights = new Color();
            
            // Computing the current biome index
                var biomeIndex = (lookUpTableSize.y - 1 - (region.y >> 1)) * lookUpTableSize.x + (region.x >> 1);
            
            // PURE BIOME
            if (region.x % 2 == 0 && region.y % 2 == 0)
            {
                // Setting the only biome index
                indices.r = biomeIndex;
                
                // Setting the weights
                weights.r = -1;
            }
            // BLENDING ON X AXIS
            else if (region.x % 2 != 0 && region.y % 2 == 0)
            {
                // Computing the index of biome right-side of the current one
                var rightBiomeIndex = biomeIndex + 1;
                
                // Setting the indices -> The current biome and the right neighbor
                indices.r = biomeIndex;
                indices.g = rightBiomeIndex;
                
                // Setting the weights
                weights.r = fractal.x;
                weights.g = -1;
            }
            // BLENDING ON Y AXIS
            else if (region.x % 2 == 0 && region.y % 2 != 0)
            {
                // Computing the index of biome above the current one
                var upperBiomeIndex = biomeIndex - lookUpTableSize.x;
                
                // Setting the indices -> The current biome and the upper neighbor
                indices.r = biomeIndex;
                indices.b = upperBiomeIndex;
                
                // Setting the weight
                weights.r = -1;
                weights.g = fractal.y;
            }
            // BLENDING ON BOTH X AND Y AXIS
            else if (region.x % 2 != 0 && region.y % 2 != 0)
            {
                // Computing the neighbors biomes
                var upperBiomeIndex = biomeIndex - lookUpTableSize.x;
                var rightBiomeIndex = biomeIndex + 1;
                var upperRightBiomeIndex = upperBiomeIndex + 1;
                
                // Setting the indices
                indices.r = biomeIndex;
                indices.g = rightBiomeIndex;
                indices.b = upperBiomeIndex;
                indices.a = upperRightBiomeIndex;
                
                // Setting the weight
                weights.r = fractal.x;
                weights.g = fractal.y;
            }

            // Storing computed value in the textures
            biomesIndices.SetPixel(x, y, indices);
            blendingWeights.SetPixel(x, y, weights);
        }
        
        // Applying changes to the texture
        biomesIndices.Apply();
        blendingWeights.Apply();

        return (biomesIndices, blendingWeights);
    }

    public Texture2DArray GetColorMaps()
    {
        // Initializing the array of texture
        var colorMaps = new Texture2DArray(Biome.heightColorMapResolution, 1, LUT.Length, 
            TextureFormat.RGBA32, false);
        
        // Auxiliary counter
        var counter = 0;
        
        // Iterating the LUT
        for (var y = 0; y < LUT.GetLength(1); y++)
        for (var x = 0; x < LUT.GetLength(0); x++)
        {
            // TODO: Improve
            // Obtaining the colormap texture from the biome
            var colormap = LUT[x, y].GetHeightColorMap();
            
            // Blitting the biomes textures in the array
            Graphics.CopyTexture(colormap, 0, 0, colorMaps, counter++, 0);
            
            // Destroying the colormaps
            if(Application.isPlaying)
                Destroy(colormap);
            else
                DestroyImmediate(colormap);
        }
            
        
        // Filling the GPU side
        colorMaps.Apply();

        return colorMaps;
    }

    public GraphicsBuffer GetHeightRanges()
    {
        // Initializing the array of min max heights
        var heightRanges = new GraphicsBuffer(GraphicsBuffer.Target.Structured, LUT.Length, 2 * sizeof(float));
        var auxiliaryArray = new Vector2 [LUT.Length];
        
        // Auxiliary counter
        var counter = 0;
        
        // Iterating the LUT
        for (var y = 0; y < LUT.GetLength(1); y++)
        for (var x = 0; x < LUT.GetLength(0); x++)
            // Computing the minimum height obtainable by the biome parameter
            auxiliaryArray[counter++] = FractalBrownianMotion.ComputeHeightBounds(LUT[x, y].noiseParameters);
        
        // Filling the GPU side
        heightRanges.SetData(auxiliaryArray);

        return heightRanges;
    }
    
    public GraphicsBuffer GetParametersBuffer()
    {
        // Initializing the buffer
        var parametersBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, LUT.Length, 
            Marshal.SizeOf(typeof(FractalBrownianMotion.Parameters)));
        
        // Creating the CPU side array
        var temp = new FractalBrownianMotion.Parameters[LUT.Length];
        
        // Auxiliary counter
        var counter = 0;

        // Iterating the LUT
        for (var y = 0; y < LUT.GetLength(1); y++)
        for (var x = 0; x < LUT.GetLength(0); x++)
            // Adding each biome parameters
            temp[counter++] = LUT[x, y].noiseParameters;
        
        // Pushing the data in the array
        parametersBuffer.SetData(temp);

        return parametersBuffer;
    }
}