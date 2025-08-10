using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "[SFG] Fractal", menuName = "SFGs/Fractal Noise")]
public class FractalScalarFieldGenerator : ASFG
{
    public int octaves;
    public float startingFrequency;
    public float startingAmplitude;
    public float lacunarity;
    public float gain;

    public override float[,,] GenerateScalarField(Vector3[,,] voxelsPositions, Vector3Int voxelsGridSize)
    {
        // Initialization
        var field = new float[voxelsGridSize.x + 1, voxelsGridSize.y + 1, voxelsGridSize.z + 1];
        
        // Generating value
        for (var x = 0; x <= voxelsGridSize.x; x++)
        for (var y = 0; y <= voxelsGridSize.y; y++)
        for (var z = 0; z <= voxelsGridSize.z; z++)
        {
            // Computing the current position
            var position = voxelsPositions[x, y, z];
            
            // Computing the noise value
            var value = Fractal(position);
            
            // Caching the value
            field[x, y, z] = value;
        }
        
        return field;
    }

    private float Fractal(Vector3 position)
    {
        // Variables
        var value = 0.0f;
        var currentAmplitude = startingAmplitude;
        var currentFrequency = startingFrequency;
        
        // Summing up octaves
        for (var i = 0; i < octaves; i++)
        {
            // Adding current Perlin noise
            value += Mathf.PerlinNoise(position.y * currentFrequency,
                Mathf.PerlinNoise(position.x * currentFrequency, position.z * currentFrequency)) * currentAmplitude;
            
            // Modifying the scalars for next octave
            currentFrequency *= lacunarity;
            currentAmplitude *= gain;
        }

        return value;
    }
}