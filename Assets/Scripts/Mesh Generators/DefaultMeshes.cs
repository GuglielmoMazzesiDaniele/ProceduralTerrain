using UnityEngine;

public class DefaultMeshes
{
    public static Mesh FlatMesh(float size, int resolution)
    {
        // Computing the total amount of vertices
        var verticesAmount = (resolution + 1) * (resolution + 1);
        
        // Initializing the array of vertices and uvs
        var vertices = new Vector3[verticesAmount];
        var uvs = new Vector2[verticesAmount];
        
        // Build triangles
        var triangles = new int[resolution * resolution * 6];
        
        // Initializing auxiliary index
        var vertexIndex = 0;
        var triangleIndex = 0;
        
        // Creating the triangles position
        for (var z = 0; z <= resolution; z++)
        for (var x = 0; x <= resolution; x++)
        {
            // Computing the UV coordinates
            var u = (float) x / resolution;
            var v = (float) z / resolution;
            
            // Storing the values
            vertices[vertexIndex] = new Vector3(u * size, 0, v * size);
            uvs[vertexIndex] = new Vector2(u, v);
            
            // Increasing the vertex index
            vertexIndex++;
        }

        // Creating the triangles array
        for (var z = 0; z < resolution; z++)
        for (var x = 0; x < resolution; x++)
        {
            // Computing the indices of the vertices
            var bottomLeft = z * (resolution + 1) + x;
            var bottomRight = bottomLeft + 1;
            var topLeft = bottomLeft + resolution + 1;
            var topRight = topLeft + 1;

            // Pushing the triangles
            triangles[triangleIndex++] = bottomLeft;
            triangles[triangleIndex++] = topLeft;
            triangles[triangleIndex++] = bottomRight;

            triangles[triangleIndex++] = bottomRight;
            triangles[triangleIndex++] = topLeft;
            triangles[triangleIndex++] = topRight;
        }
        
        // Initializing the mesh
        return new Mesh
        {
            // Storing the data into the mesh
            vertices = vertices,
            uv = uvs,
            triangles = triangles
        };
    }
}