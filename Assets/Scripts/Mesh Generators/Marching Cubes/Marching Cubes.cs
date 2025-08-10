using UnityEngine;

public class MarchingCubes : MonoBehaviour
{
    [Header("Chunks")] 
    public Vector3Int chunksAmount;
    public Vector3Int chunksGridSize;
    public GameObject chunkPrefab;
    
    [Header("Marching Cubes")]
    public float voxelSize;
    public float isoLevel;
    public bool smoothNormals = true;
    
    [Header("Scalar Field")]
    public ASFG scalarFieldGenerator;

    private MarchingCubesChunk[,,] _chunks;

    public void GenerateChunks()
    {
        // Destroying the current terrain
        ClearChunks();
        
        // Initializing the chunks array
        _chunks = new MarchingCubesChunk[chunksAmount.x, chunksAmount.y, chunksAmount.z];

        // Iteration
        for (var x = 0; x < chunksAmount.x; x++)
        for (var y = 0; y < chunksAmount.y; y++)
        for (var z = 0; z < chunksAmount.z; z++)
        {
            // Creating the current chunk prefab
            var currentChunk = Instantiate(chunkPrefab, transform);
            
            // Extracting the chunk generator component
            var chunk = currentChunk.GetComponent<MarchingCubesChunk>();
            
            // Initializing the chunk and caching it
            chunk.Initialize(new Vector3Int(x, y, z), chunksGridSize, voxelSize, 
                isoLevel, smoothNormals, scalarFieldGenerator);
            _chunks[x, y, z] = chunk;
        }
    }

    private void ClearChunks()
    {
        // Destroying all child GameObjects (chunks)
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
}
