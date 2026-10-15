using UnityEngine;

[DisallowMultipleComponent]
public sealed class GarageGPSMarker : MonoBehaviour
{
    private const float BeamHeight = 80f;
    private const float BeamWidth = 3f;
    private static readonly Color BeamColor = new Color(0.04f, 0.56f, 0.96f);
    private GameObject markerRoot;

    public void SetSelected(bool selected)
    {
        if (selected && markerRoot == null)
            CreateMarker();
        if (markerRoot != null)
            markerRoot.SetActive(selected);
    }

    private void CreateMarker()
    {
        markerRoot = new GameObject("Repere GPS Garage");
        markerRoot.transform.SetParent(transform, false);
        Vector3 parentScale = transform.lossyScale;
        markerRoot.transform.localScale = new Vector3(
            1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
            1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));

        JojoVisuals.CreateBeacon(markerRoot.transform, BeamColor, BeamHeight, BeamWidth);
    }
}
