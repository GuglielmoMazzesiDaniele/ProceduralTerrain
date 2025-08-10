using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MarchingCubes))]
public class MarchingCubesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();

        // Adding buttons
        var meshGenerator = (MarchingCubes)target;
        if (GUILayout.Button("Generate Mesh"))
        {
            meshGenerator.GenerateChunks();
        }
    }
}