using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "Textures/Generated Noise Texture", fileName = "Noise Texture")]
public class NoiseTextureAsset : ScriptableObject
{
    [Range(8, 1024)] public int size;
    public Texture2D texture;

    [Button("Generate Texture")]
    public void Generate()
    {
        texture = new Texture2D(size, size, TextureFormat.RFloat, false, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var freq = 8f;
                var value = WrapNoise(x / (float)size * freq, y / (float)size * freq, freq);
                texture.SetPixel(x, y, new Color(value, value, value));
            }
        }

        texture.Apply();
    }
    
    float WrapNoise(float x, float y, float size)
    {
        var xWrap = Mathf.PerlinNoise(x, y);
        var xOffset = Mathf.PerlinNoise(x + size, y);
        var yWrap = Mathf.PerlinNoise(x, y + size);
        var diag = Mathf.PerlinNoise(x + size, y + size);

        // Bilinear blend
        return Mathf.Lerp(
            Mathf.Lerp(xWrap, xOffset, x / size),
            Mathf.Lerp(yWrap, diag, x / size),
            y / size);
    }
}