using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Sirenix.OdinInspector;

public class RenderFeatureFog : ScriptableRendererFeature
{
    [SerializeField]
    private FogSettings settings;
    [SerializeField] 
    private Shader shader;
    private Material _material;
    private RenderPassFog _renderPassFog;
    
    [Serializable]
    public class FogSettings
    {
        [TitleGroup("Fog")] [Range(0.0f, 1.0f)]
        public float fogDensity;
        [TitleGroup("Fog")]
        public float heavyFogDistance;
        
        [TitleGroup("Noise")]
        public Texture2D noiseTexture;
        [TitleGroup("Noise")]
        public float noiseScale;
        [TitleGroup("Noise")]
        public float noiseScroll;

        [TitleGroup("SSCS")] 
        public int SSCSRadius;
        [TitleGroup("SSCS")] 
        public float SSCSIntensity;
    }
    
    public override void Create()
    {
        // If the shader is not provided, do not include the render feature
        if(!shader)
            return;
        
        // Initializing the material and the pass
        _material = new Material(shader);
        _renderPassFog = new RenderPassFog(_material, settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // If the initialization of the pass failed, return
        if(_renderPassFog == null)
            return;
        
        renderer.EnqueuePass(_renderPassFog);
    }

    protected override void Dispose(bool disposing)
    {
        if(Application.isPlaying)
            Destroy(_material);
        else
            DestroyImmediate(_material);
        
    }
}
