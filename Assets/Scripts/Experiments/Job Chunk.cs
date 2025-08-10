using System.Collections;
using Sirenix.OdinInspector;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    [HideLabel] [InlineProperty]
    public FractalBrownianMotion.Parameters parameters;
    public Material material;
    
    [HideInInspector]
    public int resolution;
    [HideInInspector]
    public int size;
    
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    
    private int _verticesCount;
    private int _indicesCount;
    private bool _isGenerating;
    
    private void Start()
    {
        // Caching 
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        
        // Computing the min and max height of the chunk
        var minMaxHeight = FractalBrownianMotion.ComputeHeightBounds(parameters);
        
        // Initializing the mesh
        var bounds = new Bounds();
        bounds.SetMinMax(new Vector3(0, minMaxHeight.x, 0), new Vector3(size, minMaxHeight.y, size));
        _mesh = new Mesh
        {
            name = "Testing Mesh",
            bounds = bounds
        };
        
        // Setting the mesh filter and renderer
        _meshFilter.mesh = _mesh;
        _meshRenderer.material = material;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(_meshRenderer.bounds.center, _meshRenderer.bounds.size);
    }

    private void Update()
    {
        // Verifying if a generation is already happening
        if (_isGenerating)
            return;
        _isGenerating = true;
        
        // Updating auxiliary variables the vertex count
        _verticesCount = (resolution + 1) * (resolution + 1);
        _indicesCount = resolution * resolution * 6;
        
        // Allocating the height array
        var heights = new NativeArray<float>(_verticesCount, Allocator.TempJob);
        
        // Initializing the height job
        var heightJob = new HeightJob
        {
            resolution = resolution,
            size = size,
            origin = new float2(transform.position.x + Time.time, transform.position.z + Time.time),
            parameters = parameters,
            heights = heights
        };

        var heightJobHandler = heightJob.ScheduleParallelByRef(_verticesCount, 32, default);
        
        // Allocating the mesh data
        var meshDataArray = Mesh.AllocateWritableMeshData(1);
        
        // Extracting the first element
        var meshData = meshDataArray[0];
        
        // Setting the format of the mesh data
        meshData.SetVertexBufferParams(_verticesCount, 
            new VertexAttributeDescriptor(VertexAttribute.Position),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
        meshData.SetIndexBufferParams(_indicesCount, IndexFormat.UInt32);
        
        // Creating the job
        var meshJob = new FlatMeshJob
        {
            heights = heights,
            resolution = resolution,
            size = size,
            meshData = meshData
        };
        
        // Scheduling the mesh job
        var meshJobHandle = meshJob.ScheduleByRef(heightJobHandler);
        JobHandle.ScheduleBatchedJobs();
        
        // Starting the coroutine that waits for the ending of the job
        StartCoroutine(CompleteMeshJob(meshJobHandle, meshDataArray, heights));
    }

    private IEnumerator CompleteMeshJob(JobHandle jobHandle, 
        Mesh.MeshDataArray meshDataArray, NativeArray<float> heights)
    {
        // Yielding until job is completed
        yield return new WaitUntil(() => jobHandle.IsCompleted);
        
        // Making sure the job is actually finished
        jobHandle.Complete();
        
        // Extracting the first element
        var meshData = meshDataArray[0];
        
        // Setting the last settings in the mesh
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, _indicesCount));
        
        // Clearing the mesh
        _mesh.Clear();
        
        // Applying the mesh data
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _mesh);
        
        // Assigning to filter and renderer
        _meshFilter.mesh = _mesh;
        
        // Signaling that generating has been completed
        _isGenerating = false;
        
        // Disposing of the native array
        heights.Dispose();
    }
    
    [BurstCompile]
    private struct HeightJob : IJobFor
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
    private struct FlatMeshJob : IJob
    {
        // Inputs
        [Unity.Collections.ReadOnly] 
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