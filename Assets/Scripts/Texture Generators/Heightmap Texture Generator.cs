using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Physics;
using Material = UnityEngine.Material;

public class HeightmapTextureGenerator : MonoBehaviour
{
    public Material targetMaterial;
    
    public int textureResolution;

    public float frequency;
    public float amplitude;
    public float octaves;
    public float lacunarity;
    public float gain;

    [Button("Generate Heightmap")]
    public void GenerateHeightmap()
    {
        // Creating the texture
        var heightmap = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        
        for(var x = 0; x < textureResolution; x++)
        for (var y = 0; y < textureResolution; y++)
        {
            var currentFrequency = frequency;
            var currentAmplitude = amplitude;
            var height = 0.0f;
            
            for (var k = 0; k < octaves; k++)
            {
                var noise = Mathf.PerlinNoise(x * currentFrequency, y * currentFrequency) * currentAmplitude;
                height += Mathf.Abs(noise * 2.0f - 1.0f);
                currentFrequency *= lacunarity;
                currentAmplitude *= gain;
            }
            
            heightmap.SetPixel(x, y, new Color(height, height, height, 1));
        }

        heightmap.Apply();

        targetMaterial.mainTexture = heightmap;
    }
    
}
