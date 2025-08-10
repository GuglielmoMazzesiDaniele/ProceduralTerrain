using UnityEngine;

public class CustomMath
{
    public static float TunableSmoothstep(float minimum, float maximum, float weight, int n)
    {
        // Computing the delta
        var delta = maximum - minimum;

        // Mapping the weight to its actual value
        var updated_weight = Mathf.Pow(weight, n) /
                               (Mathf.Pow(weight, n) + Mathf.Pow(1 - weight, n));

        // Returning the mapped value
        return minimum + delta * updated_weight;
    }
}
