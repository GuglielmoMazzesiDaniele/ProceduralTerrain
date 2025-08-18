using System;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public abstract class AChunk : MonoBehaviour
{
    [Title("Settings")]
    public int resolution;
    public int size;
    
    [Title("Rendering")]
    public bool renderAABB;
    public Material material;
    
    protected MeshFilter _meshFilter;
    protected MeshRenderer _meshRenderer;
    protected Mesh _mesh;
    
    protected int _verticesCount;
    protected int _indicesCount;
    
    # region Unity's Callback Functions
    
    private void Start ()
    {
        OnStart();
    }

    private void OnDrawGizmos()
    {
        if(renderAABB)
            Gizmos.DrawWireCube(_meshRenderer.bounds.center, _meshRenderer.bounds.size);
    }

    private void OnValidate()
    {
        OnValidateWrapper();
    }

    #endregion

    protected virtual void OnStart()
    {
        // Caching 
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        
        // Setting the mesh filter and renderer
        _meshRenderer.material = material;
        
        // Computing auxiliary variables
        _verticesCount = (resolution + 1) * (resolution + 1);
        _indicesCount = resolution * resolution * 6;
    }

    protected virtual void OnValidateWrapper()
    {
        // Updating the settings
        _verticesCount = (resolution + 1) * (resolution + 1);
        _indicesCount = resolution * resolution * 6;
    }
}