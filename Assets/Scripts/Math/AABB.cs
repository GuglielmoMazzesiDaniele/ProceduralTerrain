using UnityEngine;

public class AABB
{
    private readonly Vector3 _min;
    private readonly Vector3 _max;

    public Vector3 Center => _min + (_max - _min) / 2.0f;
    public Vector3 Size => _max - _min;

    public AABB(Vector3 min, Vector3 max)
    {
        _min = min;
        _max = max;
    }

    public bool DoesIntersectSphere(Vector3 center, float radius)
    {
        // Initializing the sqrDistance
        var sqrDistance = 0.0f;

        // X axis
        if (center.x < _min.x)
            sqrDistance += (_min.x - center.x) * (_min.x - center.x);
        else if (center.x > _max.x)
            sqrDistance += (center.x - _max.x) * (center.x - _max.x);

        // Y axis
        if (center.y < _min.y)
            sqrDistance += (_min.y - center.y) * (_min.y - center.y);
        else if (center.y > _max.y)
            sqrDistance += (center.y - _max.y) * (center.y - _max.y);

        // Z axis
        if (center.z < _min.z)
            sqrDistance += (_min.z - center.z) * (_min.z - center.z);
        else if (center.z > _max.z)
            sqrDistance += (center.z - _max.z) * (center.z - _max.z);

        return sqrDistance <= radius * radius;
    }
}