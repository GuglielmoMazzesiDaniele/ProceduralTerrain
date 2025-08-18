using UnityEngine;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;

public static class TerrainJobs
{
    [BurstCompile]
    public struct HeightJob : IJobFor
    {
        public int resolution;
        public int size;
        public float2 origin;

        public FractalBrownianMotion.Parameters parameters;

        [WriteOnly] 
        public NativeArray<float> heights;

        public void Execute(int id)
        {
            // Computing the vertices resolution
            var verticesResolution = resolution + 1;
            
            // Deriving the x and z of the thread
            var x = id % verticesResolution;
            var z = id / verticesResolution;
            
            // Computing the step size
            var stepSize = size / (float) resolution;

            // Computing world position
            var worldPos = origin + new float2(x, z) * stepSize;
            
            // Computing the height
            heights[id] = FractalBrownianMotion.FBM(parameters, worldPos);
        }
    }

    [BurstCompile]
    public struct FlatMeshJob : IJob
    {
        // Inputs
        [ReadOnly] 
        public NativeArray<float> heights;
        public int resolution;
        public int size;

        private struct Vertex
        {
            public float3 position;
            public float2 uv;
        }

        // Outputs
        public Mesh.MeshData meshData;

        public void Execute()
        {
            // Extracting the arrays from the mesh
            var vertices = meshData.GetVertexData<Vertex>();
            
            // Computing the amount of triangles
            var indices = meshData.GetIndexData<uint>();
            
            // Initializing auxiliary index
            var vertexIndex = 0;
            var triangleIndex = 0;
            
            // Building the vertex buffer
            for (var z = 0; z <= resolution; z++)
            for (var x = 0; x <= resolution; x++)
            {
                // Computing the UV coordinates
                var u = (float) x / resolution;
                var v = (float) z / resolution;
                
                // Initializing the new vertex
                var newVertex = new Vertex
                {
                    position = new float3(u * size, heights[z * (resolution + 1) + x], v * size),
                    uv = new float2(u, v)
                };
                
                // Updating the vertex's value
                vertices[vertexIndex++] = newVertex;
                
                // Verifying if I reached meshes boundaries
                if (x == resolution || z == resolution)
                    continue;
                
                // Computing the indices of the vertices
                var bottomLeft = z * (resolution + 1) + x;
                var bottomRight = bottomLeft + 1;
                var topLeft = bottomLeft + resolution + 1;
                var topRight = topLeft + 1;

                // Pushing the triangles
                indices[triangleIndex++] = (uint) bottomLeft;
                indices[triangleIndex++] = (uint) topLeft;
                indices[triangleIndex++] = (uint) bottomRight;
                
                indices[triangleIndex++] = (uint) bottomRight;
                indices[triangleIndex++] = (uint) topLeft;
                indices[triangleIndex++] = (uint) topRight;
            }
        }
    }
}
