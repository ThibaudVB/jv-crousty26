using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class EssentialAssetChecks
{
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);Debug.Log("ASSET_OK: "+label);}
    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var car=Resources.Load<GameObject>("Crousty/Vehicles/1377 Car");
            var atlas=Resources.Load<Texture2D>("Crousty/Vehicles/1377 Car");
            Check(atlas!=null,"Original car atlas exists");
            foreach(var renderer in car.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)
                Check(material.shader.name=="Universal Render Pipeline/Lit"&&material.GetTexture("_BaseMap")==atlas,"1377 car uses its original atlas in URP");
            var city=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/CroustyCity_PastelPalette_V3_Unity.fbx");
            int count=0;
            foreach(var filter in city.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.name!="Garage_01"&&filter.name!="Garage_02"&&filter.name!="Garage_03")continue;
                count++;
                var matrix=city.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var materials=filter.GetComponent<Renderer>().sharedMaterials;
                int kept=0;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    if(!materials[sub].name.EndsWith("Repair_Glow"))continue;
                    var indices=mesh.GetTriangles(sub);kept+=indices.Length;
                    Check(indices.All(i=>matrix.MultiplyPoint3x4(vertices[i]).y<1),filter.name+" floating marker triangles removed");
                }
                Check(kept>0,filter.name+" floor repair marker preserved");
                var go=new GameObject(filter.name);go.transform.position=matrix.GetColumn(3);go.transform.rotation=matrix.rotation;go.transform.localScale=matrix.lossyScale;
                var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
                var b=collider.bounds;
                Vector3 center=b.center;
                // Door positions differ between the three garages. At least one closed
                // side must physically block a ray from outside; the roof must remain solid.
                int walls=0;
                foreach(var dir in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                {
                    var start=new Vector3(center.x,6,center.z)+dir*30;
                    if(collider.Raycast(new Ray(start,-dir),out var hit,30))walls++;
                }
                Check(walls>=2,filter.name+" building side collisions preserved");
                Check(collider.Raycast(new Ray(new Vector3(center.x,20,center.z),Vector3.down),out var roof,15),filter.name+" roof collision preserved");
                UnityEngine.Object.DestroyImmediate(go);
            }
            Check(count==3,"All three garage variants validated");Debug.Log("ESSENTIAL_ASSET_CHECKS_DONE failures=0");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
