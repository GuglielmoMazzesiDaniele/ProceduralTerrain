using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

public class TerrainGenerator : MonoBehaviour
{
    #region Editor Fields

    [Title("Automatic Procedural Generation")]
    public int levelsOfDetail;
    public int chunksPerDirection;

    [Title("Chunk")]
    public int chunkResolution;
    public int startingChunkSize;
    
    [Title("Terrain Chunk")]
    public GameObject terrainChunkPrefab;
    public bool proceduralScattering;
    public int scatterAmount;
    
    [Title("Rendering")]
    public Material terrainMaterial;

    [Title("Terrain Generators")] 
    public ComputeShader computeShader;

    [Title("Biomes")] 
    public BiomesLUT biomes;
    
    # endregion
    
    # region Private Fields
    
    private struct ChunkMetaData
    {
        public int slice;
        public Vector3 origin;

        // -- TERRAIN ---
        public GameObject terrainGameObject;
        public MeshRenderer terrainMeshRenderer;
        public MeshFilter terrainMeshFilter;
        public TerrainChunk terrainChunk;
    }
    
    private struct LevelOfDetail
    {
        // SHARED
        public int chunkSize;
        public Mesh sharedMesh;
        public int level;
        public Vector2Int previousRegenerationCoords;
        
        // POOLS
        public Dictionary<Vector2Int, ChunkMetaData> gridToChunk;
        public Stack<ChunkMetaData> chunksPool;
        
        // TEXTURES
        public RenderTexture terrainHeightmaps;
        public RenderTexture terrainNormalmaps;
        public RenderTexture terrainBiomemaps;
    }

    private int _totalChunksPerLOD;
    private bool _isInitialized;
    
    private Camera _mainCamera;
    private readonly uint[] _minMaxHeight = new uint[2];

    private LevelOfDetail[] LODs;
    
    private readonly int _heightmapsMaterialID = Shader.PropertyToID("_HeightMaps");
    private readonly int _normalmapsMaterialID = Shader.PropertyToID("_NormalMaps");
    private readonly int _biomemapsMaterialID = Shader.PropertyToID("_BiomeMaps");
    private readonly int _biomesGradientsMaterialID = Shader.PropertyToID("_BiomesGradient");
    private readonly int _biomesHeightRangesMaterialID = Shader.PropertyToID("_BiomesHeightRanges");
    private readonly int _sliceIndexMaterialID = Shader.PropertyToID("_SliceIndex");
    
    # endregion
    
    # region Compute Shader Fields
    
    private int _terrainHeightmapKernelID;
    private int _terrainNormalmapKernelID;
    private int _proceduralScatteringKernelID;
    private int _frustumCullingKernelID;

    private int _groupsPerAxis2D;
    private int _groupsCount;

    private Texture2DArray biomesGradients;
    private GraphicsBuffer _biomesParametersBuffer;
    private GraphicsBuffer _biomesHeightRangesBuffer;
    private GraphicsBuffer _minMaxHeightBuffer;
    private GraphicsBuffer _frustumPlanesBuffer;
    private GraphicsBuffer _visibleScatterCounterBuffer;
    
    private readonly int _terrainHeightmapsID = Shader.PropertyToID("terrain_heightmaps");
    private readonly int _normalmapsID = Shader.PropertyToID("normalmaps");
    private readonly int _biomemapsID = Shader.PropertyToID("biomemaps");
    
    private readonly int _sliceIndexID = Shader.PropertyToID("slice_index");
    private readonly int _lutSizeID = Shader.PropertyToID("lut_size");
    private readonly int _minMaxHeightID = Shader.PropertyToID("min_max_height");
    private readonly int _biomesParametersID = Shader.PropertyToID("biomes_params");
    private readonly int _biomesColorGradientID = Shader.PropertyToID("biomes_color_gradient");
    private readonly int _frustumPlanesID = Shader.PropertyToID("frustum_planes");
    private readonly int _visibleScatterCounterID = Shader.PropertyToID("visible_scatter_counter");
    private readonly int _scatterAmountID = Shader.PropertyToID("scatter_amount");
    private readonly int _chunkOriginID = Shader.PropertyToID("chunk_origin");
    private readonly int _chunkSizeID = Shader.PropertyToID("chunk_size");
    private readonly int _chunkResolutionID = Shader.PropertyToID("chunk_resolution");

    # endregion
    
    # region Unity's Callback Functions
    private void Awake()
    {
        if(!_isInitialized)
            InitializeGenerator();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;
        
        // Caching the camera position
        var cameraPos = _mainCamera.transform.position;
        
        // Iterating all levels of detail
        for (var i = 0; i < LODs.Length; i++)
        {
            // Caching a reference to the LOD
            ref var LOD = ref LODs[i];
            
            // Computing the current camera grid coordinates
            var cameraGridCoords = WorldToHorizontalGrid(cameraPos,LOD.chunkSize);
            
            // Updating the terrain
            UpdateChunks(cameraGridCoords, LOD);
        }
    }

    private void OnDestroy()
    {
        // Deallocating the buffer and the render textures
        _minMaxHeightBuffer?.Release();
        _visibleScatterCounterBuffer?.Release();
        _biomesParametersBuffer?.Release();
        _frustumPlanesBuffer?.Release();
        
        // Destroying the textures
        if (Application.isPlaying)
        {
            Destroy(biomesGradients);

        } else
        {
            DestroyImmediate(biomesGradients);
        }
        
        ClearChildren();
    }
    
    # endregion

    # region Initialization
    
    [Title("Buttons")]
    private void InitializeGenerator()
    {
        // Caching the main camera
        _mainCamera = Camera.main;
        
        // Computing some auxiliary variable
        _totalChunksPerLOD = (chunksPerDirection * 2 + 1) * (chunksPerDirection * 2 + 1);
        
        // Initializing the terrain compute shader
        InitializeComputeShader();
        
        // Initializing the level of details
        InitializeLOD();
        
        // Swapping flag
        _isInitialized = true;
    }
    
    private void InitializeComputeShader()
    {
        // Setting constant buffers
        computeShader.SetInt(_chunkResolutionID, chunkResolution);
        computeShader.SetInt(_scatterAmountID, scatterAmount);
        computeShader.SetVector(_lutSizeID, new Vector3(biomes.lookUpTableSize.x, biomes.lookUpTableSize.y));
        
        // Caching the kernel ids
        _terrainHeightmapKernelID = computeShader.FindKernel("terrain_heightmap");
        _terrainNormalmapKernelID = computeShader.FindKernel("generate_normalmap");
        _proceduralScatteringKernelID = computeShader.FindKernel("procedural_scattering");
        _frustumCullingKernelID = computeShader.FindKernel("scattering_frustum_culling");
        
        // Computing threads per group
        _groupsPerAxis2D = Mathf.CeilToInt((chunkResolution + 1) / 16f);
        _groupsCount = _groupsPerAxis2D * _groupsPerAxis2D;
        
        // Storing a reference to the colormaps
        biomesGradients = biomes.GetColorMaps();
        computeShader.SetTexture(_terrainHeightmapKernelID, _biomesColorGradientID, biomesGradients);
        
        // Initializing buffers
        _biomesParametersBuffer = biomes.GetParametersBuffer();
        _biomesHeightRangesBuffer = biomes.GetHeightRanges();
        _minMaxHeightBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _groupsCount, sizeof(uint) * 2);
        _frustumPlanesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 6, sizeof(float) * 4);
        _visibleScatterCounterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Counter, 1, sizeof(uint));
        
        // Assigning buffers to kernels
        computeShader.SetBuffer(_terrainHeightmapKernelID, _biomesParametersID, _biomesParametersBuffer);
        computeShader.SetBuffer(_terrainHeightmapKernelID, _minMaxHeightID, _minMaxHeightBuffer);
        computeShader.SetBuffer(_proceduralScatteringKernelID, _biomesParametersID, _biomesParametersBuffer);
        computeShader.SetBuffer(_frustumCullingKernelID, _visibleScatterCounterID, _visibleScatterCounterBuffer);
        computeShader.SetBuffer(_frustumCullingKernelID, _frustumPlanesID, _frustumPlanesBuffer);
    }

    private void InitializeLOD()
    {
        // Initializing the array
        LODs = new LevelOfDetail[levelsOfDetail];
        
        // Initializing each level of detail
        for (var i = 0; i < LODs.Length; i++)
        {
            // Caching the LOD
            ref var LOD = ref LODs[i];
            
            // Initializing some simple values
            LOD.level = i;
            LOD.chunkSize = startingChunkSize * (1 << i);
            LOD.sharedMesh = DefaultMeshes.FlatMesh(LODs[i].chunkSize, chunkResolution);
            LOD.previousRegenerationCoords = new Vector2Int(int.MaxValue, int.MaxValue);
            
            // Initializing the terrain dictionaries and pools
            LOD.gridToChunk = new Dictionary<Vector2Int, ChunkMetaData> ();
            LOD.chunksPool = new Stack<ChunkMetaData> ();
            
            // Initializing the heightmaps array
            LOD.terrainHeightmaps = new RenderTexture(chunkResolution + 1, chunkResolution + 1, 0)
            {
                format = RenderTextureFormat.RFloat,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = _totalChunksPerLOD,
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            
            // Initializing the normalmaps array
            LOD.terrainNormalmaps = new RenderTexture(chunkResolution + 1, chunkResolution + 1, 0)
            {
                format = RenderTextureFormat.ARGBHalf,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = _totalChunksPerLOD,
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            
            // Initializing the biomemaps
            LOD.terrainBiomemaps = new RenderTexture(chunkResolution + 1, chunkResolution + 1, 0)
            {
                format = RenderTextureFormat.RGFloat,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = _totalChunksPerLOD,
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            
            // Warming the pool
            InitializePool(LOD);
        }
    }

    private void InitializePool(LevelOfDetail LOD)
    {
        // Creating the terrain material property block
        var terrainMPB = new MaterialPropertyBlock();
        terrainMPB.SetTexture(_heightmapsMaterialID, LOD.terrainHeightmaps);
        terrainMPB.SetTexture(_normalmapsMaterialID, LOD.terrainNormalmaps);
        terrainMPB.SetTexture(_biomemapsMaterialID, LOD.terrainBiomemaps);
        terrainMPB.SetTexture(_biomesGradientsMaterialID, biomesGradients);
        terrainMPB.SetBuffer(_biomesHeightRangesMaterialID, _biomesHeightRangesBuffer);
        terrainMPB.SetVector(_lutSizeID, new Vector3(biomes.lookUpTableSize.x, biomes.lookUpTableSize.y));
        
        // Warming the pool
        for (var i = 0; i < _totalChunksPerLOD; i++)
        {
            // Creating the chunk metadata
            var chunkMetaData = new ChunkMetaData
            {
                slice = i
            };
            
            // Setting the slice in the material property block
            terrainMPB.SetInt(_sliceIndexMaterialID, i);

            // Instantiating the prefab and disabling it
            var terrainChunkGO = Instantiate(terrainChunkPrefab, transform);
            terrainChunkGO.SetActive(false);
            
            // Initial setup and caching of components
            chunkMetaData.terrainGameObject = terrainChunkGO;
            
            // Assigning the mesh to the mesh filter
            chunkMetaData.terrainMeshFilter = terrainChunkGO.GetComponent<MeshFilter>();
            chunkMetaData.terrainMeshFilter.mesh = LOD.sharedMesh;
            
            // Assigning the material's properties to the mesh renderer
            chunkMetaData.terrainMeshRenderer = terrainChunkGO.GetComponent<MeshRenderer>();
            chunkMetaData.terrainMeshRenderer.sharedMaterial = terrainMaterial;
            chunkMetaData.terrainMeshRenderer.SetPropertyBlock(terrainMPB);
            
            // Assigning values to the terrain chunk and doing some initialization
            chunkMetaData.terrainChunk = terrainChunkGO.GetComponent<TerrainChunk>();
            chunkMetaData.terrainChunk.scatterAmount = scatterAmount;
            chunkMetaData.terrainChunk.sliceIndex = i;
            chunkMetaData.terrainChunk.InitializeComputeShader();
            
            // Pushing the metadata in the pool
            LOD.chunksPool.Push(chunkMetaData);
        }
    }
    
    # endregion
    
    # region Terrain
    
    private void UpdateChunks(Vector2Int cameraGridCoords, LevelOfDetail LOD)
    {
        // Updating the compute shader variables to current LOD
        computeShader.SetFloat(_chunkSizeID, LOD.chunkSize);
        computeShader.SetTexture(_terrainHeightmapKernelID, _terrainHeightmapsID, LOD.terrainHeightmaps);
        computeShader.SetTexture(_terrainHeightmapKernelID, _biomemapsID, LOD.terrainBiomemaps);
        computeShader.SetTexture(_terrainNormalmapKernelID, _terrainHeightmapsID, LOD.terrainHeightmaps);
        computeShader.SetTexture(_terrainNormalmapKernelID, _normalmapsID, LOD.terrainNormalmaps);
        computeShader.SetTexture(_proceduralScatteringKernelID, _normalmapsID, LOD.terrainNormalmaps);
        
        // If the camera moved into a new chunk, verify if terrain needs expansion
        if (cameraGridCoords != LOD.previousRegenerationCoords)
        {
            // Finding which chunk needs to be removed
            List<Vector2Int> toRemove = new();
            
            // Iterating all chunks
            foreach (var chunkEntry in LOD.gridToChunk)
            {
                // Computing the grid coordinates of the current offset
                var gridCoords = chunkEntry.Key;
                var offset = gridCoords - cameraGridCoords;
                
                // Verifying that the chunk is within valid range
                var tooFar = Mathf.Abs(offset.x) > chunksPerDirection
                             || Mathf.Abs(offset.y) > chunksPerDirection;
                var inHole  = IsWithinHigherLOD(LOD, offset);

                // If it is in an invalid position, queue it for removal
                if (tooFar || inHole)
                    toRemove.Add(gridCoords);
            }
            
            // Destroying distant chunks
            foreach (var gridCoords in toRemove)
            {
                // Despawning the chunk
                DespawnChunk(gridCoords, LOD);
            }
            
            // Iterating neighbours chunks, spawning the one that have not been generated yet
            for (var z = -chunksPerDirection; z <= chunksPerDirection; z++)
            for (var x = -chunksPerDirection; x <= chunksPerDirection; x++)
            {
                // Computing the offset
                var offset = new Vector2Int(x, z);
                
                // If I am within a higher LOD, do not spawn
                if (IsWithinHigherLOD(LOD, offset))
                    continue;
                
                // Computing the grid position in world space
                var gridCoords = cameraGridCoords + offset;

                // If the chunk has already been generated, continue
                if (LOD.gridToChunk.ContainsKey(gridCoords))
                    continue;
                
                SpawnChunk(gridCoords, LOD);
            }
        }

        if (proceduralScattering && LOD.level == 0)
        {
            // Creating the current frustum planes and pushing them on GPU
            var planes = GeometryUtility.CalculateFrustumPlanes(_mainCamera);
            var frustumPlanes = new Vector4[6];
            for (var k = 0; k < 6; k++)
                frustumPlanes[k] = new Vector4(planes[k].normal.x, 
                    planes[k].normal.y, 
                    planes[k].normal.z, 
                    planes[k].distance);
            _frustumPlanesBuffer.SetData(frustumPlanes);
        
            // Iterating all chunks
            foreach (var chunkMetaData in LOD.gridToChunk)
            {
                // Resetting the counter of visible scatter
                _visibleScatterCounterBuffer.SetCounterValue(0);
        
                // Culling and drawing scatter on current chunk, delegated
                chunkMetaData.Value.terrainChunk.CullAndDraw(_visibleScatterCounterBuffer);
            }
        }

        // Storing the current position for the next frame
        LOD.previousRegenerationCoords = cameraGridCoords;
    }
    
    private void SpawnChunk(Vector2Int gridCoords, LevelOfDetail LOD)
    {
        // Getting a free chunk from the pool
        if (LOD.chunksPool.Count <= 0)
            throw new Exception("Chunk pool ran out of availability!");
        
        // Extracting an element from the pools and reactivating the game objects
        var chunkMetaData = LOD.chunksPool.Pop();
        
        // Computing and caching the chunk origin
        chunkMetaData.origin = GridToWorld(gridCoords, LOD.chunkSize);
        
        // Setting the value of the chunk origin
        computeShader.SetVector(_chunkOriginID, chunkMetaData.origin);
        computeShader.SetInt(_sliceIndexID, chunkMetaData.slice);
        
        // --- TERRAIN ---
        
        // Reactivating the terrain game object
        chunkMetaData.terrainGameObject.SetActive(true);
        
        // Resetting the min max buffer on GPU
        _minMaxHeightBuffer.SetData(new [] {0xFFFFFFFF, 0x00000000u});
        
        // Dispatching the heightmap kernel
        computeShader.Dispatch(_terrainHeightmapKernelID, _groupsPerAxis2D, 1, _groupsPerAxis2D);
        
        // Dispatching the heightmap kernel
        computeShader.Dispatch(_terrainNormalmapKernelID, _groupsPerAxis2D, 1, _groupsPerAxis2D);
        
        // Extracting the min max from the groups
        _minMaxHeightBuffer.GetData(_minMaxHeight);
        var globalMin = SortableUintToFloat(_minMaxHeight[0]);
        var globalMax = SortableUintToFloat(_minMaxHeight[1]);

        // Computing the new world bounds
        var bounds = new Bounds();
        bounds.SetMinMax(chunkMetaData.origin + new Vector3(0, globalMin, 0),
            chunkMetaData.origin + new Vector3(LOD.chunkSize, globalMax, LOD.chunkSize));
        chunkMetaData.terrainMeshRenderer.bounds = bounds;
        
        // Updating some variables
        chunkMetaData.terrainGameObject.transform.position = chunkMetaData.origin;
        chunkMetaData.terrainGameObject.name = $"Chunk [{gridCoords.x}, " + $"{gridCoords.y}] - LOD " + LOD.level;
        
        // Reactivating the chunk
        chunkMetaData.terrainChunk.ReactivateChunk();
        
        // Adding the entries to the dictionaries
        LOD.gridToChunk[gridCoords] = chunkMetaData;
    }
    
    private static void DespawnChunk(Vector2Int gridCoords, LevelOfDetail LOD)
    {
        // Removing the chunk from the pool
        if (!LOD.gridToChunk.Remove(gridCoords, out var chunkMetaData)) 
            throw new Exception("Mismatch between grid coordinates and chunk's GameObject!");
        chunkMetaData.terrainGameObject.SetActive(false);
        
        // Pushing back into the pool
        LOD.chunksPool.Push(chunkMetaData);
    }
    
    # endregion
    
    # region Auxiliary Functions
    
    private static Vector2Int WorldToHorizontalGrid(Vector3 worldPos, float cellSize)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x / cellSize),
            Mathf.FloorToInt(worldPos.z / cellSize));
    }

    private static Vector3 GridToWorld(Vector2Int gridPosition, float cellSize)
    {
        return new Vector3(gridPosition.x * cellSize, 0, gridPosition.y * cellSize);
    }
    
    private static float SortableUintToFloat(uint s)
    {
        var u = (s & 0x80000000u) != 0
            ? (s ^ 0x80000000u)
            : ~s;
        // Reinterpreting bits directly
        return BitConverter.Int32BitsToSingle((int)u);
    }
    
    private bool IsWithinHigherLOD(LevelOfDetail LOD, Vector2Int offset)
    {
        // LOD 0: no hole
        if (LOD.level == 0) 
            return false;

        // sizes for this LOD and the one just below it
        float prevSize = LODs[LOD.level - 1].chunkSize;
        float curSize  = LODs[LOD.level].chunkSize;

        // previous LOD covers from prevMin → prevMax in world space
        var prevMin = -chunksPerDirection * prevSize;
        var prevMax =  chunksPerDirection * prevSize + prevSize;

        // this chunk’s world‑space X and Z extents
        var curMinX = offset.x * curSize;
        var curMaxX = curMinX + curSize;
        var curMinZ = offset.y * curSize;
        var curMaxZ = curMinZ + curSize;

        // true if chunk is *entirely* inside the previous LOD’s box
        var insideX = curMinX > prevMin && curMaxX < prevMax;
        var insideZ = curMinZ > prevMin && curMaxZ < prevMax;

        // skip only if both axes are fully inside
        return insideX && insideZ;
    }
    
    [Button("Clear Children")]
    private void ClearChildren()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isEditor)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject); 
        }
    }
    
    # endregion
}
