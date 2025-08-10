using NUnit.Framework;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using Bounds = UnityEngine.Bounds;

public class TerrainChunk : MonoBehaviour
{
    [Title("Procedural Scattering")]
    public Mesh mesh;
    public Material material;
    public ComputeShader computeShader;
    public bool drawBounds;
    
    [HideInInspector] public int sliceIndex;
    [HideInInspector] public int scatterAmount;
    
    private MeshRenderer _meshRenderer;
    
    private GraphicsBuffer _matricesBuffer;
    private GraphicsBuffer _visibleMatricesBuffer;
    private GraphicsBuffer _indirectDrawCommandBuffer;
    private RenderParams _scatterRenderParameters;
    
    private int _proceduralScatteringKernelID;
    private int _scatteringFrustumCullingKernelID;

    private readonly int _transformsMaterialID = Shader.PropertyToID("_Transforms");
    private readonly int _visibleTransformsMaterialID = Shader.PropertyToID("_VisibleTransforms");
    
    private readonly int _transformsID = Shader.PropertyToID("transforms");
    private readonly int _visibleTransformsID = Shader.PropertyToID("visible_transforms");

    private readonly int _chunkOriginID = Shader.PropertyToID("chunk_origin");
    private readonly int _sliceIndexID = Shader.PropertyToID("slice_index");

    # region Unity's Callback Functions
    private void Awake()
    {
        // Initialization and caching of auxiliary variables
        InitializeFields();
    }

    private void OnDrawGizmos()
    {
        if(drawBounds)
            Gizmos.DrawWireCube(_meshRenderer.bounds.center, _meshRenderer.bounds.size);
    }

    private void OnDestroy()
    {
        _matricesBuffer?.Release();
        _visibleMatricesBuffer?.Release();
        _indirectDrawCommandBuffer?.Release();
    }

    # endregion
    
    private void InitializeFields()
    {
        // Caching some stuff
        _meshRenderer = gameObject.GetComponent<MeshRenderer>();
        
        // Creating the command buffer used to dispatch indirect draw calls
        _indirectDrawCommandBuffer ??= new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size);
    }

    public void InitializeComputeShader()
    {
        // Caching some IDs
        _proceduralScatteringKernelID = computeShader.FindKernel("procedural_scattering");
        _scatteringFrustumCullingKernelID = computeShader.FindKernel("scattering_frustum_culling");
        
        // Initializing the buffers
        _matricesBuffer ??= new GraphicsBuffer(GraphicsBuffer.Target.Structured,
            scatterAmount, sizeof(float) * 16);
        _visibleMatricesBuffer ??= new GraphicsBuffer(GraphicsBuffer.Target.Structured,
            scatterAmount, sizeof(float) * 16);
    }

    public void ReactivateChunk()
    {
        // Updating the data of the computer shaders
        computeShader.SetBuffer(_proceduralScatteringKernelID, _transformsID, _matricesBuffer);
        computeShader.SetInt(_sliceIndexID, sliceIndex);
        computeShader.SetVector(_chunkOriginID, transform.position);
        
        // Dispatching the kernel
        computeShader.Dispatch(_proceduralScatteringKernelID, 
            Mathf.CeilToInt(scatterAmount / 256f), 1, 1);
        
        // Material property block
        var materialPropertyBlock = new MaterialPropertyBlock();

        // Binding the GPU‑filled matrices buffer
        materialPropertyBlock.SetBuffer(_transformsMaterialID, _matricesBuffer);
        materialPropertyBlock.SetBuffer(_visibleTransformsMaterialID, _visibleMatricesBuffer);

        // Building scatter render parameters
        _scatterRenderParameters = new RenderParams(material)
        {
            matProps = materialPropertyBlock,
            worldBounds = new Bounds(_meshRenderer.bounds.center, _meshRenderer.bounds.size)
        };
        
        // Filling the command buffer
        var args = new GraphicsBuffer.IndirectDrawIndexedArgs {
            indexCountPerInstance = mesh.GetIndexCount(0),
            instanceCount = 0,
            startIndex = mesh.GetIndexStart(0),
            baseVertexIndex = mesh.GetBaseVertex(0),
            startInstance = 0
        };
        _indirectDrawCommandBuffer.SetData(new []{ args });
    }

    public void CullAndDraw(GraphicsBuffer visibleCounter)
    {
        // Filling the command buffer
        var indirectDrawCommands = new [] { 
            new GraphicsBuffer.IndirectDrawIndexedArgs {
                indexCountPerInstance = mesh.GetIndexCount(0),
                instanceCount = (uint) scatterAmount,
                startIndex = mesh.GetIndexStart(0),
                baseVertexIndex = mesh.GetBaseVertex(0),
                startInstance = 0 
            }
        };
        _indirectDrawCommandBuffer.SetData(indirectDrawCommands);
        
        // Updating the data of the computer shaders
        computeShader.SetBuffer(_scatteringFrustumCullingKernelID, _transformsID, _matricesBuffer);
        computeShader.SetBuffer(_scatteringFrustumCullingKernelID, _visibleTransformsID, _visibleMatricesBuffer);
    
        // Dispatching the cull kernel
        computeShader.Dispatch(_scatteringFrustumCullingKernelID,
            Mathf.CeilToInt(scatterAmount / 256f), 1, 1);
        
        GraphicsBuffer.CopyCount(visibleCounter, _indirectDrawCommandBuffer, sizeof(uint));
        
        // Executing the draw call
        Graphics.RenderMeshIndirect(_scatterRenderParameters, mesh, _indirectDrawCommandBuffer);
    }
}

