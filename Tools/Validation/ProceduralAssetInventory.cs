using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProceduralAssetInventory
{
    public static void Run()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/CroustyCity_PastelPalette_V3_Unity.fbx");
        var output = new StringBuilder("name\tparent\tcenter\tsize\tminY\tdeterminant\n");
        foreach (var filter in asset.GetComponentsInChildren<MeshFilter>(true))
        {
            var matrix = asset.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var b = filter.sharedMesh.bounds;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) bounds.Encapsulate(matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            output.AppendLine(filter.name + "\t" + filter.transform.parent.name + "\t" + bounds.center.ToString("F2") + "\t" + bounds.size.ToString("F2") + "\t" + bounds.min.y + "\t" + matrix.determinant);
        }
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "asset-inventory.tsv"), output.ToString());
        EditorApplication.Exit(0);
    }
}
