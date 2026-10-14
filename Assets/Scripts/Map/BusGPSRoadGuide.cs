using System.Collections.Generic;
using UnityEngine;

// Reusable ground arrows: no colliders, no per-frame mesh creation, no drawing through walls.
[DisallowMultipleComponent]
[RequireComponent(typeof(BusGPS))]
public sealed class BusGPSRoadGuide : MonoBehaviour
{
    private BusGPS gps;
    private ProceduralCity city;
    private Transform root;
    private Mesh mesh;
    private Material material;
    private readonly List<GameObject> arrows = new List<GameObject>(32);
    private float nextRefresh;

    private void Awake()
    {
        gps = GetComponent<BusGPS>();
        city = FindAnyObjectByType<ProceduralCity>();
        root = new GameObject("GPS - fleches sur la chaussee").transform;
        root.SetParent(transform, false);
        mesh = new Mesh { name = "Fleche GPS au sol" };
        mesh.vertices = new[] {
            new Vector3(0f, 0f, 2f), new Vector3(-1.1f, 0f, .4f), new Vector3(-.35f, 0f, .4f),
            new Vector3(-.35f, 0f, -1.4f), new Vector3(.35f, 0f, -1.4f),
            new Vector3(.35f, 0f, .4f), new Vector3(1.1f, 0f, .4f) };
        mesh.triangles = new[] { 1, 0, 6, 2, 5, 3, 3, 5, 4 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var template = Resources.Load<Material>("Crousty/ProceduralSignal");
        material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = new Color(.05f, .85f, 1f);
        material.SetColor("_EmissionColor", new Color(.05f, .85f, 1f) * 2f);
        material.EnableKeyword("_EMISSION");
    }

    private void Update()
    {
        if (!gps.isActiveAndEnabled || gps.Target == null || !gps.Target.IsWaiting || gps.Route.Count < 2 ||
            gps.deliveries == null || gps.deliveries.HasFailed)
        { root.gameObject.SetActive(false); return; }
        root.gameObject.SetActive(true);
        if (Time.timeScale <= 0f || Time.time < nextRefresh) return;
        nextRefresh = Time.time + .15f;
        int used = 0;
        float traveled = 0f, nextArrow = 4f;
        for (int segment = 1; segment < gps.Route.Count && traveled < 200f && used < 32; segment++)
        {
            Vector3 from = gps.Route[segment - 1], to = gps.Route[segment];
            Vector3 delta = to - from; delta.y = 0f;
            float length = delta.magnitude;
            if (length < .01f) continue;
            while (nextArrow <= traveled + length && nextArrow <= 200f && used < 32)
            {
                Vector3 position = Vector3.Lerp(from, to, (nextArrow - traveled) / length);
                if (city == null || city.IsAreaReady(position))
                {
                    var arrow = GetArrow(used++);
                    arrow.transform.SetPositionAndRotation(position + Vector3.up * .12f, Quaternion.LookRotation(delta));
                    arrow.SetActive(true);
                }
                nextArrow += 8f;
            }
            traveled += length;
        }
        for (int i = used; i < arrows.Count; i++) arrows[i].SetActive(false);
    }

    private GameObject GetArrow(int index)
    {
        if (index < arrows.Count) return arrows[index];
        var arrow = new GameObject("Direction GPS", typeof(MeshFilter), typeof(MeshRenderer));
        arrow.transform.SetParent(root, false);
        arrow.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = arrow.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        arrows.Add(arrow);
        return arrow;
    }

    private void OnDisable() { if (root != null) root.gameObject.SetActive(false); }
    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
