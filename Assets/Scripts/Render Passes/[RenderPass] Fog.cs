using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class RenderPassFog : ScriptableRenderPass
{
    // Postprocessing shader's 
    private readonly int _fogColorID = Shader.PropertyToID("_FogColor");
    private readonly int _fogDensityID = Shader.PropertyToID("_FogDensity");
    private readonly int _heavyFogDistance = Shader.PropertyToID("_HeavyFogDistance");
    
    private readonly int _noiseTexID = Shader.PropertyToID("_NoiseTex");
    private readonly int _noiseScaleID = Shader.PropertyToID("_NoiseScale");
    private readonly int _noiseScrollID = Shader.PropertyToID("_NoiseScroll");

    private readonly int _SSCSTexelSizeID = Shader.PropertyToID("_SSCS_TexelSize");
    private readonly int _SSCSRadiusID = Shader.PropertyToID("_SSCS_Radius");
    private readonly int _SSCSIntensityID = Shader.PropertyToID("_SSCS_Intensity");
    
    private readonly RenderFeatureFog.FogSettings _settings;
    private readonly Material _material;
    
    private TextureDesc _fogTextureDescriptor;

    public RenderPassFog(Material material, RenderFeatureFog.FogSettings settings)
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
        _fogTextureDescriptor = currentCameraColor.GetDescriptor(renderGraph);
        _fogTextureDescriptor.name = "_FogTexture";
        _fogTextureDescriptor.depthBufferBits = 0;
        var destinationTexture = renderGraph.CreateTexture(_fogTextureDescriptor);
        
        // Updating the variables of the material
        UpdateSetting(frameData.Get<UniversalCameraData>().camera);
        
        // First pass: Applying fog to destination texture
        RenderGraphUtils.BlitMaterialParameters fogBlit = new(currentCameraColor, destinationTexture, _material, 0);
        renderGraph.AddBlitPass(fogBlit, "FogRenderPass");
        
        // Second pass: Copying fogged result back to camera color target
        renderGraph.AddCopyPass(destinationTexture, currentCameraColor, 0, 0, 0, 0, "FogCopyBaskPass");
    }

    private void UpdateSetting(Camera camera)
    {
        // If the material is not set, return
        if(!_material)
            return;
        
        // FOG
        _material.SetColor(_fogColorID, Color.gray);
        _material.SetFloat(_fogDensityID, _settings.fogDensity);
        _material.SetFloat(_heavyFogDistance, _settings.heavyFogDistance);
        
        // NOISE
        _material.SetTexture(_noiseTexID, _settings.noiseTexture);
        _material.SetFloat(_noiseScaleID, _settings.noiseScale);
        _material.SetFloat(_noiseScrollID, _settings.noiseScroll);
        
        // SSCS
        _material.SetVector(_SSCSTexelSizeID, new Vector2(1f / camera.pixelWidth, 1f / camera.pixelHeight));
        _material.SetInt(_SSCSRadiusID, _settings.SSCSRadius);
        _material.SetFloat(_SSCSIntensityID, _settings.SSCSIntensity);
    }
}
