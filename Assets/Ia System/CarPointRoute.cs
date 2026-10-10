using UnityEngine;

// Ordered points for CarFollowPoints; no navigation mesh is required.
public sealed class CarPointRoute : MonoBehaviour
{
    public CarPointRoute nextRoute;
    public IntersectionChooser intersection;
}
