using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class RenderPassWater : ScriptableRenderPass
{
    private readonly int _waterHeightID = Shader.PropertyToID("_Height");
    
    private readonly int _shallowWaterID = Shader.PropertyToID("_ShallowWater");
    private readonly int _deepWaterID = Shader.PropertyToID("_DeepWater");
    private readonly int _densityID = Shader.PropertyToID("_Density");
    private readonly int _maxDepthID = Shader.PropertyToID("_MaxDepth");

    private readonly int _firstNormalMapID = Shader.PropertyToID("_FirstNormalMap");
    private readonly int _secondNormalMapID = Shader.PropertyToID("_SecondNormalMap");
    private readonly int _normalMapScalingID = Shader.PropertyToID("_NormalMapScaling");
    private readonly int _waterShininessID = Shader.PropertyToID("_Shininess");
    
    private readonly RenderFeatureWater.WaterSettings _settings;
    private readonly Material _material;
    
    private TextureDesc _waterTextureDescriptor;

    public RenderPassWater(Material material, RenderFeatureWater.WaterSettings settings)
    {
        _material = material;
        _settings = settings;
    }
    
    public override void RecordRenderGraph(RenderGraph renderGraph,
        ContextContainer frameData)
    {
        // Caching the current frame texture
        var resourceData = frameData.Get<UniversalResourceData>();
        
        // Avoiding blit from the back buffer
        if (resourceData.isActiveTargetBackBuffer)
            return;
        
        // Creating the texture handler on which the shader will run
        var currentCameraColor = resourceData.activeColorTexture;
        
        // Setting the variables for the fog texture and initializing it
        _waterTextureDescriptor = currentCameraColor.GetDescriptor(renderGraph);
        _waterTextureDescriptor.name = "_WaterTexture";
        _waterTextureDescriptor.depthBufferBits = 0;
        var destinationTexture = renderGraph.CreateTexture(_waterTextureDescriptor);
        
        // Updating the variables of the material
        UpdateSetting(frameData.Get<UniversalCameraData>().camera);
        
        // First pass: Applying fog to destination texture
        RenderGraphUtils.BlitMaterialParameters fogBlit = new(currentCameraColor, destinationTexture, _material, 0);
        renderGraph.AddBlitPass(fogBlit, "WaterRenderPass");
        
        // Second pass: Copying fogged result back to camera color target
        renderGraph.AddCopyPass(destinationTexture, currentCameraColor, 0, 0, 0, 0, "WaterCopyBaskPass");
    }

    private void UpdateSetting(Camera camera)
    {
        // If the material is not set, return
        if(!_material)
            return;

        // Positioning
        _material.SetFloat(_waterHeightID, _settings.height);
        
        // Shading
        _material.SetColor(_shallowWaterID, _settings.shallowWater);
        _material.SetColor(_deepWaterID, _settings.deepWater);
        _material.SetFloat(_densityID, _settings.density);
        _material.SetFloat(_maxDepthID, _settings.maxDepth);
        
        // Rendering
        _material.SetTexture(_firstNormalMapID, _settings.firstNormalmap);
        _material.SetTexture(_secondNormalMapID, _settings.secondNormalmap);
        _material.SetFloat(_normalMapScalingID, _settings.normalmapScaling);
        _material.SetFloat(_waterShininessID, _settings.shininess);
    }
}
