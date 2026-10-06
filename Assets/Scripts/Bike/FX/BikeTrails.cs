using UnityEngine;

namespace MotoSquid.Bike
{
    
public static class BikeTrails
{
    public static void Clear(Transform root)
    {
        if (root == null) return;
        var trails = root.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
            if (trails[i] != null) trails[i].Clear();
    }
}
}
