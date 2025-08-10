using System;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "Biome", menuName = "Biomes/Biome")]
public class Biome : SerializedScriptableObject
{
    [InlineProperty] [HideLabel]
    public FractalBrownianMotion.Parameters noiseParameters;

    [Title("Rendering")]
    public Vector2 heightRange;
    public Gradient heightColorMap;
    
    public const int heightColorMapResolution = 256;
    
    public Texture2D GetHeightColorMap()
    {
        // Initializing the texture
        var colorMapTexture = new Texture2D(heightColorMapResolution, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
            
        // Fill each pixel from the Gradient
        for (var i = 0; i < heightColorMapResolution; i++)
        {
            var color = heightColorMap.Evaluate(i / (heightColorMapResolution - 1f));
            colorMapTexture.SetPixel(i, 0, color);
        }
        colorMapTexture.Apply();

        return colorMapTexture;
    }
}