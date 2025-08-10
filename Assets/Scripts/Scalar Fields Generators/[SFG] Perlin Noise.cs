using System;
using UnityEngine;

[CreateAssetMenu(fileName = "[SFG] Perlin", menuName = "SFGs/Perlin Noise")]
public class PerlinScalarFieldGenerator : ASFG
{
    public float frequency;
    public float amplitude;

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
            
            // Sampling Perlin Noise at the position
            var noiseValue = Mathf.PerlinNoise(position.y * frequency, 
                Mathf.PerlinNoise(position.x * frequency, position.z * frequency));
            
            // Caching the value
            field[x, y, z] = noiseValue * amplitude;
        }

        return field;
    }
}
