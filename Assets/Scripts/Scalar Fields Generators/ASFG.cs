using UnityEngine;

public abstract class ASFG : ScriptableObject
{
    public abstract float[,,] GenerateScalarField(Vector3[,,] voxelsPositions, Vector3Int voxelsGridSize);
}