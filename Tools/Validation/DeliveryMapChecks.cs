using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
public static class DeliveryMapChecks
{
 static int checks;
 static string scene;
 static void Check(bool pass, string label) { if (!pass) throw new Exception(label); checks++; Debug.Log("DELIVERY PASS: " + label); }
 static Vector3 Position(string id) {
  var block=Regex.Match(scene,@"(?ms)^--- !u!4 &"+id+@"\r?\n.*?(?=^---|\z)").Value;
  var match=Regex.Match(block,@"m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}");
  return new Vector3(float.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),float.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture),float.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture));
 }
 public static void Run() {
  try {
   string sourceRoot=Directory.GetCurrentDirectory();
   string[] args=Environment.GetCommandLineArgs();
   int sourceIndex=Array.IndexOf(args,"-croustySourceProject");
   if(sourceIndex>=0 && sourceIndex+1<args.Length) sourceRoot=args[sourceIndex+1];
   scene=File.ReadAllText(Path.Combine(sourceRoot,"Assets/SceneBase.unity"));
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/CroustyCity_PastelPalette_V3_Unity.fbx");
   Check(prefab!=null,"new map imports");
   var map=UnityEngine.Object.Instantiate(prefab); map.transform.position=Position("1890655281");
   foreach(var collider in map.GetComponentsInChildren<Collider>()) if(collider.name=="Road_Markings") collider.enabled=false;
   Physics.SyncTransforms();
   var pickup=Array.Find(map.GetComponentsInChildren<Transform>(),t=>t.name=="Pickup_Crousty");
   Vector3 shopPosition=map.transform.position+Position("1200941629");
   Check(pickup!=null && Vector3.Distance(pickup.position,shopPosition)<.01f,"shop matches imported Pickup_Crousty anchor");
   var sources=new List<NavMeshBuildSource>();
   NavMeshBuilder.CollectSources(map.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
   var renderers=map.GetComponentsInChildren<Renderer>(); Bounds bounds=renderers[0].bounds;
   foreach(var r in renderers) bounds.Encapsulate(r.bounds); bounds.Expand(10);
   var settings=NavMesh.CreateSettings(); settings.agentRadius=2;settings.agentHeight=4;settings.agentClimb=.3f;settings.agentSlope=35;settings.overrideVoxelSize=true;settings.voxelSize=.4f;
   var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
   Check(data!=null,"GPS navigation builds from new map colliders");
   var instance=NavMesh.AddNavMeshData(data);
   var filter=new NavMeshQueryFilter {agentTypeID=settings.agentTypeID,areaMask=NavMesh.AllAreas};
   Check(NavMesh.SamplePosition(Position("21662979"),out var start,6,filter),"bus spawn on navigation");
   var shop=new GameObject("Shop test").AddComponent<CroustyShop>();shop.transform.position=shopPosition;shop.pickupRadius=16;
   Check(NavMesh.SamplePosition(shopPosition,out var shopRoad,16,filter),"road near pickup");
   Check(shop.IsInRange(shopRoad.position+Vector3.up),"orders can load from road");
   Check(!shop.IsInRange(shopPosition+Vector3.up*10),"no loading from elevated route");
   string[] ids={"2120590645","1389575106","229347457","1779071069","1345296050","428393978"};
   foreach(var id in ids){
    Vector3 pos=map.transform.position+Position(id);
    var point=new GameObject(id).AddComponent<DeliveryPoint>();point.transform.position=pos;point.deliveryRadius=12;
    Check(NavMesh.SamplePosition(pos,out var target,4,filter),id+" GPS samples within 4m");
    var path=new NavMeshPath();Check(NavMesh.CalculatePath(start.position,target.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete,id+" reachable from bus");
    Check(point.IsInRange(target.position+Vector3.up),id+" deliverable from reachable surface");
    Check(!point.IsInRange(pos+Vector3.up*10),id+" no delivery from elevated route");
    Check(Physics.Raycast(pos+Vector3.up*2,Vector3.down,out var ground,4)&&Mathf.Abs(ground.point.y-pos.y)<.3f,id+" marker at ground level, outside buildings");
    point.SetOrderActive(true);Check(point.IsWaiting,id+" waiting order");point.MarkPending();Check(!point.IsWaiting,id+" in-flight order cannot repeat");point.CompleteDelivery();Check(!point.IsWaiting,id+" delivered order remains complete");
   }
   File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"result.txt"),"PASSED "+checks+" checks");
   Debug.Log("DELIVERY CHECKS PASSED: "+checks);EditorApplication.Exit(0);
  } catch(Exception e) { Debug.LogException(e);File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"result.txt"),"FAILED "+e);EditorApplication.Exit(1); }
 }
}
