using System.Collections;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class ParametersChunk : AChunk
{
    [Title("Noise")] 
    public FractalBrownianMotion.Parameters parameters;

    # region Unity's Callback Functions
    private void OnValidate()
    {
        OnValidateWrapper();
    }
    
    # endregion
    
    protected override void OnStart()
    {
        // Running previous level initialization
        base.OnStart();
        
        // Generating the mesh
        UpdateMesh();
    }

    protected override void OnValidateWrapper()
    {
        // Running previous level implementation
        base.OnValidateWrapper();
        
        // Updating the mesh
        UpdateMesh();
    }

    protected void UpdateMesh()
    {
        // Computing the min and max height of the chunk
        var minMaxHeight = FractalBrownianMotion.ComputeHeightBounds(parameters);
        
        // Initializing the mesh
        var bounds = new Bounds();
        bounds.SetMinMax(new Vector3(0, minMaxHeight.x, 0), new Vector3(size, minMaxHeight.y, size));
        _mesh = new Mesh
        {
            name = "Chunk Mesh",
            bounds = bounds
        };
        
        // Allocating the height array
        var heights = new NativeArray<float>(_verticesCount, Allocator.TempJob);
        
        // Initializing the height job
        var heightJob = new TerrainJobs.HeightJob
        {
            resolution = resolution,
            size = size,
            origin = new float2(transform.position.x, transform.position.z),
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
        var meshJob = new TerrainJobs.FlatMeshJob
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
    
    protected IEnumerator CompleteMeshJob(JobHandle jobHandle,
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
        
        // Disposing of the native array
        heights.Dispose();
    }
}
