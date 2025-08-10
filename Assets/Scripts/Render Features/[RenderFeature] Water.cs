using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Sirenix.OdinInspector;

public class RenderFeatureWater : ScriptableRendererFeature
{
    [SerializeField]
    private WaterSettings settings;
    [SerializeField] 
    private Shader shader;
    private Material _material;
    private RenderPassWater _renderPassWater;

    [Serializable]
    public class WaterSettings
    {
        [Title("Positioning")]
        public float height;
        
        [Title("Shading")]
        public Color shallowWater;
        public Color deepWater;
        public float density;
        public float maxDepth;
        
        [Title("Rendering")]
        public float shininess;
        public float normalmapScaling;
        public Texture2D firstNormalmap;
        public Texture2D secondNormalmap;
    }
    
    public override void Create()
    {
        // If the shader is not provided, do not include the render feature
        if(!shader)
            return;
        
        // Initializing the material and the pass
        _material = new Material(shader);
        _renderPassWater = new RenderPassWater(_material, settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // If the initialization of the pass failed, return
        if(_renderPassWater == null)
            return;
        
        renderer.EnqueuePass(_renderPassWater);
    }

    protected override void Dispose(bool disposing)
    {
        if(Application.isPlaying)
            Destroy(_material);
        else
            DestroyImmediate(_material);
        
    }
    
}
