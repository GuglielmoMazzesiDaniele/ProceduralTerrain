using System.Collections.Generic;
using UnityEngine;

public class MarchingCubesChunk : MonoBehaviour
{
    private Vector3Int _chunkOrigin;
    
    private MeshFilter _meshFilter;
    private ASFG _scalarFieldGenerator;
    
    private Vector3 _center;
    private Vector3Int _gridSize;
    
    private float _voxelSize;
    private float _isoLevel;
    
    private float[,,] _scalarValues;

    private bool _smoothNormals;
    
    # region Unity's Callback Functions
    
    private void OnDrawGizmos()
    {
        // Generating a gizmo to visualize the spanned area
        Gizmos.DrawWireCube(transform.TransformPoint(0.5f * (Vector3)_gridSize), _gridSize);
    }
    
    # endregion
    
    /// <summary>
    /// Cache the mesh components attached to the chunk GO, and add them if they are not present.
    /// </summary>
    private void CacheComponents()
    {
        // Adding a Mesh Filter to the game object (or caching it)
        _meshFilter = GetComponent<MeshFilter>();
        if (_meshFilter == null)
            _meshFilter = gameObject.AddComponent<MeshFilter>();
        
        // Adding a Mesh Renderer to the game object 
        if (!GetComponent<MeshRenderer>())
            gameObject.AddComponent<MeshRenderer>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="chunkIndex"></param>
    /// <param name="gridSize"></param>
    /// <param name="voxelSize"></param>
    /// <param name="isoLevel"></param>
    /// <param name="scalarFieldGenerator"></param>
    public void Initialize(Vector3Int chunkIndex, Vector3Int gridSize, float voxelSize, float isoLevel,
        bool smoothNormals, ASFG scalarFieldGenerator)
    {
        // Caching the provided data
        _chunkOrigin = chunkIndex;
        _gridSize = gridSize;
        _voxelSize = voxelSize;
        _isoLevel = isoLevel;
        _smoothNormals = smoothNormals;
        _scalarFieldGenerator = scalarFieldGenerator;
        
        // Changing the transform of the chunk
        transform.position = new Vector3(
            _chunkOrigin.x * _gridSize.x,
            _chunkOrigin.y * _gridSize.y,
            _chunkOrigin.z * _gridSize.z
        );
        
        // Caching components
        CacheComponents();
        
        // Generate the mesh
        GenerateMesh();
    }

    /// <summary>
    /// Generates a mesh using the marching cube algorithm and a scalar
    /// </summary>
    private void GenerateMesh()
    {
        // Computing the voxels grid size
        var voxelsGridSize = new Vector3Int(
            Mathf.CeilToInt(_gridSize.x / _voxelSize),
            Mathf.CeilToInt(_gridSize.y / _voxelSize),
            Mathf.CeilToInt(_gridSize.z / _voxelSize)
        );
        
        // Creating the positions that need to be sampled
        var voxelsPosition = new Vector3[voxelsGridSize.x + 1, voxelsGridSize.y + 1, voxelsGridSize.z + 1];
        var startingPosition = transform.position;
        
        for (var x = 0; x <= voxelsGridSize.x; x++)
        for (var y = 0; y <= voxelsGridSize.y; y++)
        for (var z = 0; z <= voxelsGridSize.z; z++)
        {
            voxelsPosition[x, y, z] = startingPosition + _voxelSize * new Vector3(x, y, z);
        }

        // Caching variables from the scalar field generator
        _scalarValues = _scalarFieldGenerator.GenerateScalarField(voxelsPosition, voxelsGridSize);
        
        // Initializing the list of vertices and triangles
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var normals = new List<Vector3>();

        // Iterating all vertices
        for (var x = 0; x < voxelsGridSize.x; x++) 
        for (var y = 0; y < voxelsGridSize.y; y++) 
        for (var z = 0; z < voxelsGridSize.z; z++)
        {
            // Initializing the position of the corners
            var cubePositions = new Vector3[8];

            // Initializing the values of the cube
            var cubeValues = new float[8];
            
            // Iterating the corners
            for (var i = 0; i < 8; i++)
            {
                // Extracting corner's offset
                var offset = MarchingCubeTables.CornerOffsets3D[i];
                
                // Computing the corner's coordinates in voxel space
                var offsetX = x + offset.x;
                var offsetY = y + offset.y;
                var offsetZ = z + offset.z;
                
                // Computing the corner's coordinates in local space
                cubePositions[i] = new Vector3(offsetX, offsetY, offsetZ) * _voxelSize;
                
                // Caching the corner's value
                cubeValues[i] = _scalarValues[offsetX, offsetY, offsetZ];
            }
            
            // Marching the cube
            MarchCube(cubePositions, cubeValues, vertices, normals, triangles);
        }

        // VerifyMeshComponents();
        
        // Creating the mesh
        var marchedMesh = new Mesh
        {
            // Setting the vertices and the triangles
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };
        
        // Setting the normals
        if (_smoothNormals)
            marchedMesh.normals = normals.ToArray();
        else
            marchedMesh.RecalculateNormals();
        
        // Pushing the mesh on the mesh filter
        _meshFilter.sharedMesh = marchedMesh;
    }

    /// <summary>
    /// March the cube defined by the input parameters.
    /// </summary>
    /// <param name="cubePositions">An array of Vector3, containing the positions of the corners</param>
    /// <param name="cubeValues">An array of floats, containing the values of the corners</param>
    /// <param name="vertices">The list of vertices, used to append new ones.</param>
    /// <param name="normals">The list of normals</param>
    /// <param name="triangles">The list of triangles, used to create the mesh.</param>
    private void MarchCube(Vector3[] cubePositions, float[] cubeValues, List<Vector3> vertices, List<Vector3> normals,
        List<int> triangles)
    {
        // Initializing the cube index
        var cubeIndex = 0;
        
        // Iterating all corners to compute the hexadecimal value of the cube index
        for(var i = 0; i < 8; i++)
            if (cubeValues[i] < _isoLevel)
                cubeIndex |= 1 << i;
        
        // Extrapolating the edges from the table using the cube index
        var crossedEdges = MarchingCubeTables.EdgeTable[cubeIndex];
        
        // Case in which the table is fully in or out of the surface (no crossed edges)
        if(crossedEdges == 0)
            return;
        
        // Initializing the list of vertices on edges (half vertices)
        var edgeVertices = new Vector3[12];
        
        // Iterating all the vertices interpolate the position of surface intersection
        for (var i = 0; i < 12; i++)
        {
            // Excluding the cases in which there is not a surface on the edge
            if ((crossedEdges & (1 << i)) == 0)
                continue;
            
            // Extrapolating the index of the first corner
            var firstIndex = MarchingCubeTables.EdgeIndexPairs[i, 0];
            var secondIndex = MarchingCubeTables.EdgeIndexPairs[i, 1];
            
            // Interpolating the position of the surface on the current edge
            edgeVertices[i] = VertexInterpolation(cubePositions[firstIndex], cubePositions[secondIndex],
                cubeValues[firstIndex], cubeValues[secondIndex]);
        }
        
        // Adding the vertices and related triangles to the mesh
        for (var i = 0; MarchingCubeTables.TriTable[cubeIndex, i] != -1; i += 3)
        {
            // Extracting the edge vertices indices from the table
            var firstIndex = MarchingCubeTables.TriTable[cubeIndex, i];
            var secondIndex = MarchingCubeTables.TriTable[cubeIndex, i + 1];
            var thirdIndex = MarchingCubeTables.TriTable[cubeIndex, i + 2];
            
            // Caching the count of the vertices list
            var verticesCount = vertices.Count;
            
            // Adding the interpolated vertices to the list
            vertices.Add(edgeVertices[firstIndex]);
            vertices.Add(edgeVertices[secondIndex]);
            vertices.Add(edgeVertices[thirdIndex]);
            
            // Adding the normals to the list
            normals.Add(EstimateNormal(edgeVertices[firstIndex]));
            normals.Add(EstimateNormal(edgeVertices[secondIndex]));
            normals.Add(EstimateNormal(edgeVertices[thirdIndex]));
            
            // Adding the triangle to the mesh
            triangles.Add(verticesCount);
            triangles.Add(verticesCount + 1);
            triangles.Add(verticesCount + 2);
        }
    }

    /// <summary>
    /// Given two positions and two values, computes an interpolated position based on the value and the iso level
    /// </summary>
    /// <param name="firstPosition">Vector3, representing first position</param>
    /// <param name="secondPosition">Vector3, representing second position</param>
    /// <param name="firstValue">Float, first value</param>
    /// <param name="secondValue">Float, second value</param>
    /// <returns>The interpolated position as a Vector3</returns>
    private Vector3 VertexInterpolation(Vector3 firstPosition, Vector3 secondPosition, float firstValue, float secondValue)
    {
        // Epsilon
        const float epsilon = 1e-5f;
        
        // Case in which the intersection is very close to first position
        if (Mathf.Abs(_isoLevel - firstValue) < epsilon)
            return firstPosition;
        
        // Case in which the intersection is very close to second position
        if (Mathf.Abs(_isoLevel - secondValue) < epsilon)
            return secondPosition;
        
        // Case in which the value on the edges is very close
        if (Mathf.Abs(firstValue - secondValue) < epsilon)
            return firstPosition;
        
        // Linear interpolation of the vertex within the edge
        return firstPosition + (_isoLevel - firstValue) / (secondValue - firstValue) * (secondPosition - firstPosition);
    }

    /// <summary>
    /// Estimate
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    private Vector3 EstimateNormal(Vector3 position)
    {
        // The delta used for central difference
        var delta = _voxelSize * 0.5f;

        return Vector3.Normalize(new Vector3(
            SampleSDF(position + Vector3.right * delta) - SampleSDF(position - Vector3.right * delta),
            SampleSDF(position + Vector3.up * delta) - SampleSDF(position - Vector3.up * delta),
            SampleSDF(position + Vector3.forward * delta) - SampleSDF(position - Vector3.forward * delta)
        ));
    }

    /// <summary>
    /// Interpolates the SDF value at given position using trilinear interpolation
    /// </summary>
    /// <param name="position">Vector3 representing the position</param>
    /// <returns>A float containing the interpolated value</returns>
    private float SampleSDF(Vector3 position)
    {
        // Converting world-space position (scaled by voxel size) to grid space
        var gridPos = position / _voxelSize;

        // Computing the grid coordinates
        var x0 = Mathf.FloorToInt(gridPos.x);
        var y0 = Mathf.FloorToInt(gridPos.y);
        var z0 = Mathf.FloorToInt(gridPos.z);
        
        // Computing the 
        var x1 = Mathf.Min(x0 + 1, _scalarValues.GetLength(0) - 1);
        var y1 = Mathf.Min(y0 + 1, _scalarValues.GetLength(1) - 1);
        var z1 = Mathf.Min(z0 + 1, _scalarValues.GetLength(2) - 1);

        // Caching the weights, used for linear interpolation
        var dx = gridPos.x - x0;
        var dy = gridPos.y - y0;
        var dz = gridPos.z - z0;

        // Clamping base indices to grid bounds
        x0 = Mathf.Clamp(x0, 0, _scalarValues.GetLength(0) - 1);
        y0 = Mathf.Clamp(y0, 0, _scalarValues.GetLength(1) - 1);
        z0 = Mathf.Clamp(z0, 0, _scalarValues.GetLength(2) - 1);

        // Sampling the values of the scalar field at the 8 corners of the cube
        var c000 = _scalarValues[x0, y0, z0];
        var c001 = _scalarValues[x0, y0, z1];
        var c010 = _scalarValues[x0, y1, z0];
        var c011 = _scalarValues[x0, y1, z1];
        var c100 = _scalarValues[x1, y0, z0];
        var c101 = _scalarValues[x1, y0, z1];
        var c110 = _scalarValues[x1, y1, z0];
        var c111 = _scalarValues[x1, y1, z1];

        // Linear interpolation on the Z axis
        var c00 = Mathf.Lerp(c000, c001, dz);
        var c01 = Mathf.Lerp(c010, c011, dz);
        var c10 = Mathf.Lerp(c100, c101, dz);
        var c11 = Mathf.Lerp(c110, c111, dz);

        // Linear interpolation on the Y axis
        var c0 = Mathf.Lerp(c00, c01, dy);
        var c1 = Mathf.Lerp(c10, c11, dy);

        // Linear interpolation on the X axis
        var value = Mathf.Lerp(c0, c1, dx);
        
        return value;
    }
}
