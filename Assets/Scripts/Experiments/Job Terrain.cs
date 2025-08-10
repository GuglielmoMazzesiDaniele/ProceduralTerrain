using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.Serialization;

public class JobTerrain : MonoBehaviour
{
    public GameObject chunkPrefab;
    public int LODAmount;
    public int startingChunkPerAxis;
    public int startingChunkSize;
    public int chunkResolution;

    private struct ChunkMeta
    {
        public GameObject gameObject;
        public Chunk chunk;
    }

    private struct LOD
    {
        public int chunkSize;
        public int chunksPerAxis;

        public int size;
        public Vector3 origin;
        public int level;

        public List<ChunkMeta> chunks;
    }
    
    private readonly List<LOD> LODs = new ();
    private Vector2Int previousFrameCoords;

    private void Start()
    {
        // Creating the dummy LOD, to avoid implementing corner case for the first actual LOD
        var dummyLOD = new LOD
        {
            chunkSize = startingChunkSize / 2,
            size = 0,
            level = -1
        };
        LODs.Add(dummyLOD);
        
        // Initializing the LODs
        for (var i = 1; i <= LODAmount; i++)
        {
            // Caching the previous LOD
            var previousLOD = LODs[i - 1];
            
            // Creating a new LOD
            var newLOD = new LOD
            {
                chunks = new List<ChunkMeta>(),
                chunkSize = previousLOD.chunkSize * 2,
                chunksPerAxis = startingChunkPerAxis,
                origin = previousLOD.origin - new Vector3(previousLOD.size / 2, 0, previousLOD.size / 2),
                level = i
            };
            
            // Computing the size of LOD in object space, used to the next LOD to avoid overlapping
            newLOD.size = newLOD.chunkSize * newLOD.chunksPerAxis;
            
            // Adding the recently created LOD
            LODs.Add(newLOD);
        }

        // Initializing the chunks
        for(var i = 1; i <= LODAmount; i++)
        {
            // Caching the current and previous LOD
            var currentLOD = LODs[i];
            var previousLOD = LODs[i - 1];
            
            // Initializing the current LOD chunks
            for (var x = 0; x < currentLOD.chunksPerAxis; x++)
            for (var z = 0; z < currentLOD.chunksPerAxis; z++)
            {
                // Computing the position in LOD space
                var posLOD = new Vector2Int(x * currentLOD.chunkSize, z * currentLOD.chunkSize);
            
                // Skipping section occupied by previous LOD
                if (posLOD.x >= previousLOD.size / 2 && posLOD.x < previousLOD.size + previousLOD.size / 2 &&
                    posLOD.y >= previousLOD.size / 2 && posLOD.y < previousLOD.size + previousLOD.size / 2)
                    continue;
                
                // Instantiating a prefab
                var chunkGO = Instantiate(chunkPrefab);

                // Initializing the metadata
                var chunkMeta = new ChunkMeta
                {
                    gameObject = chunkGO,
                    chunk = chunkGO.GetComponent<Chunk>()
                };

                // Setting some variables
                chunkMeta.gameObject.transform.position = 
                    currentLOD.origin + new Vector3(x * currentLOD.chunkSize, 0, z * currentLOD.chunkSize);
                chunkMeta.chunk.size = currentLOD.chunkSize;
                chunkMeta.chunk.resolution = chunkResolution;

                // Pushing in the array
                currentLOD.chunks.Add(chunkMeta);
            }
        }
    }
}