using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public sealed class ProceduralChecks : MonoBehaviour
{
 int count;
 // Exercise a time-budgeted streamer without depending on wall-clock frame timing.
 void Drive(ProceduralCity city,int frames) {for(int i=0;i<frames;i++){city.GetType().GetField("nextMaintenance",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(city,0f);Call(city,"Update");}}
 void Check(bool pass,string message) {if(!pass)throw new Exception(message);count++;Debug.Log("PROCEDURAL PASS: "+message);}
 void Call(object target,string method,params object[] args) {target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}
 string Fingerprint(Transform chunk) {return string.Join(";",chunk.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.GetComponentInParent<CroustyShop>()==null&&m.GetComponentInParent<DeliveryPoint>()==null).Select(m=>m.name+"/"+m.sharedMesh.name+"/"+m.transform.position.ToString("F3")+"/"+m.transform.lossyScale.ToString("F3")+"/"+m.transform.rotation.ToString("F3")));}
 IEnumerator Start() {
  yield return null;
  try {
   var city=FindAnyObjectByType<ProceduralCity>();var manager=FindAnyObjectByType<DeliveryManager>();var gps=FindAnyObjectByType<BusGPS>();
   Check(city.WorldRoot!=null,"world initializes before gameplay");Check(city.BuildingVariantCount>=20,"existing FBX building library reused");Check(city.LandmarkVariantCount>=10,"ramps and landmark assemblies reused");Check(city.Seed==city.seed&&!city.randomSeed,"fixed seed reproducible");Check(city.WorldRoot.GetComponentsInChildren<MeshFilter>().Any(m=>m.name.Contains("CROUSTY_SignText")),"Crousty signage preserved");Check(city.LoadedChunkCount>=9,"initial ground neighbourhood loaded");
   Check(city.IsCroustyBlock(0,0)&&city.IsCroustyBlock(16,0)&&!city.IsCroustyBlock(12,0),"pickup restaurants use a sparse kilometre lattice");
   Check(city.IsParkBlock(8,8)&&city.IsParkBlock(9,9)&&!city.IsParkBlock(7,8)&&city.IsParkBlock(20,8),"parks form sparse two-by-two green zones");
   Check(city.cityModel.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.name=="Police_Station"),"the supplied FBX contains the original police station");
   int policeAnchors=0;for(int x=-2;x<=-1;x++)for(int z=0;z<=1;z++)if(city.IsPoliceBlock(x,z)&&city.LandmarkNameAt(x,z)=="Commissariat")policeAnchors++;
   Check(policeAnchors==1,"one police station is assigned near spawn for this seed");
   var stationParts=city.WorldRoot.GetComponentsInChildren<MeshFilter>(true);
   Check(stationParts.Count(m=>m.name=="Visuel Police_Station")==1&&stationParts.Any(m=>m.name=="Visuel Police_FacadeText")&&stationParts.Any(m=>m.name=="Visuel Police_CanopyText"),"the complete original police station and signs are visible at spawn");
   var nearbyVenues=new HashSet<string>();for(int x=-2;x<=3;x++)for(int z=-2;z<=3;z++)if(city.IsLandmarkBlock(x,z)&&!city.IsPoliceBlock(x,z))nearbyVenues.Add(city.LandmarkNameAt(x,z));
   Check(nearbyVenues.Count>=3,"nearby special venues vary instead of repeating the same garage");
   int landmarkBlocks=0,fireRings=0;for(int mx=-50;mx<=50;mx++)for(int mz=-50;mz<=50;mz++){string landmark=city.LandmarkNameAt(mx,mz);if(landmark==null)continue;landmarkBlocks++;if(landmark.ToLowerInvariant().Contains("cercle de feu"))fireRings++;}
   Check(landmarkBlocks>0&&landmarkBlocks<10201,"landmarks stay occasional");Check(fireRings>0&&fireRings<landmarkBlocks,"fire rings exist without appearing everywhere");
   var generatedLandmark=city.WorldRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Ensemble original - Megarampe et cercle de feu");Check(generatedLandmark!=null,"initial city GUARANTEES a mega ramp and fire ring");Check(generatedLandmark.GetComponentsInChildren<MeshCollider>().Length>0,"landmark ramps preserve mesh collisions");
   Drive(city,250);
   // Content checks include the full rooftop course, beyond the circular visible radius.
   for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)Call(city,"LoadChunk",new Vector2Int(x,z));
   var meshes=city.WorldRoot.GetComponentsInChildren<MeshFilter>();
   Check(meshes.Any(m=>m.name=="Passages pietons et lignes"&&m.sharedMesh.vertexCount>100),"connected junctions have batched zebra crossings, stop lines and lane markings");
   Check(!meshes.Any(m=>m.name.StartsWith("Visuel TrafficLight_")&&m.transform.parent.name.StartsWith("Mobilier original")),"traffic lights are removed from random street furniture");
   var signals=city.WorldRoot.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Feu tricolore ")).ToArray();
   Check(signals.Length>=4&&signals.All(s=>s.GetComponentsInChildren<Renderer>().Count(r=>r.name=="Rouge"||r.name=="Orange"||r.name=="Vert")==3),"active traffic lights stand at real approaches with three visible aspects");
   var signalNode=new Vector2Int(int.MaxValue,int.MaxValue);for(int sx=-2;sx<=3&&signalNode.x==int.MaxValue;sx++)for(int sz=-2;sz<=3;sz++)if(city.RuleAt(new Vector2Int(sx,sz),Vector2Int.up)==ProceduralCity.RoadRule.TrafficLight){signalNode=new Vector2Int(sx,sz);break;}
   Check(signalNode.x!=int.MaxValue,"at least one nearby four-way junction is signal-controlled");
   bool nsGreen=false,ewGreen=false,safe=true;for(int second=0;second<30;second++){var ns=city.LightAt(signalNode,Vector2Int.up,second);var ew=city.LightAt(signalNode,Vector2Int.right,second);nsGreen|=ns==ProceduralCity.LightAspect.Green;ewGreen|=ew==ProceduralCity.LightAspect.Green;if(ns==ProceduralCity.LightAspect.Green&&ew==ProceduralCity.LightAspect.Green)safe=false;}
   Check(nsGreen&&ewGreen&&safe,"signal phases alternate with no conflicting greens");
   var redArm=city.LightAt(signalNode,Vector2Int.up,Time.time)==ProceduralCity.LightAspect.Red?Vector2Int.up:Vector2Int.right;
   var redWorld=city.Origin+new Vector3(signalNode.x*64,0,signalNode.y*64);var redCar=redWorld+new Vector3(redArm.x*20,0,redArm.y*20);
   Check(city.AllowedTrafficStep(FindObjectsByType<CarFollowPoints>()[0],redCar,redWorld+new Vector3(5,0,5),1f)==0f,"AI cars stop before a red traffic light");
   var stopNode=new Vector2Int(int.MaxValue,int.MaxValue);var stopArm=Vector2Int.zero;for(int sx=-3;sx<=5&&stopNode.x==int.MaxValue;sx++)for(int sz=-3;sz<=5;sz++)foreach(var arm in new[]{Vector2Int.right,Vector2Int.left})if(city.RuleAt(new Vector2Int(sx,sz),arm)==ProceduralCity.RoadRule.Stop){stopNode=new Vector2Int(sx,sz);stopArm=arm;break;}
   Check(stopNode.x!=int.MaxValue,"unsignalled junctions include actual STOP situations");
   var sampleCar=FindObjectsByType<CarFollowPoints>()[0];var stopWorld=city.Origin+new Vector3(stopNode.x*64,0,stopNode.y*64);var carAtLine=stopWorld+new Vector3(stopArm.x*20,0,stopArm.y*20);var cornerTarget=stopWorld+new Vector3(5,0,5);
   Check(city.AllowedTrafficStep(sampleCar,carAtLine,cornerTarget,1f)==0f,"STOP holds an approaching car at the painted line");
   var loop=city.WorldRoot.GetComponentsInChildren<CarPointRoute>()[0].transform;Check(loop.GetChild(1).localPosition.x==loop.GetChild(0).localPosition.x&&loop.GetChild(1).localPosition.z>loop.GetChild(0).localPosition.z,"procedural traffic circulates on the right");
   Check(meshes.Count(m=>m.name=="Visuel CROUSTY_Shop")==city.WorldRoot.GetComponentsInChildren<CroustyShop>().Length,"every visible Crousty is a real pickup, none in random buildings");
   Check(city.NativeBlockCount>=8&&city.StreetSetCount>=20,"native block and street furniture libraries retained");
   foreach(string required in new[]{"Route_Depart_Ramp","Route_Gradins_Ramp","BankedTrack_001__Route_Oval_Deck","Route_Helix_Tower","FireRing_001__Route_FireRing_GrandPlongeon","HalfPipeWall_001__Route_HalfPipe_Deck","LongRamp_001__Route_Descent_LongRamp"})
    Check(meshes.Any(m=>m.name=="Visuel "+required),"full connected rooftop course includes "+required);
   foreach(string kind in new[]{"Lamp_","TrafficLight_","Bench_","Bin_","BusStop_","StreetTree_","MorrisColumn_","BikeRack_"})
    Check(meshes.Any(m=>m.name.StartsWith("Visuel "+kind)),"native street variety includes "+kind);
   var mega=meshes.First(m=>m.name=="Visuel Stunt_MegaRamp");Check(mega.GetComponent<Renderer>().bounds.size.y>12,"mega ramp keeps original playable height instead of miniature scaling");
   Check(mega.GetComponent<MeshCollider>()!=null&&mega.GetComponent<MeshCollider>().sharedMesh==mega.sharedMesh,"driving collision matches actual ramp surface");
   var ramp=meshes.First(m=>m.name=="Visuel Route_Depart_Ramp");var rampBounds=ramp.GetComponent<Renderer>().bounds;
   Physics.SyncTransforms();int slopeHits=0;for(int rx=0;rx<10;rx++)for(int rz=0;rz<10;rz++){var ray=new Ray(new Vector3(Mathf.Lerp(rampBounds.min.x,rampBounds.max.x,(rx+.5f)/10),rampBounds.max.y+2,Mathf.Lerp(rampBounds.min.z,rampBounds.max.z,(rz+.5f)/10)),Vector3.down);if(ramp.GetComponent<MeshCollider>().Raycast(ray,out var hit,20)&&hit.normal.y>.55f&&hit.normal.y<.99f)slopeHits++;}
   Check(slopeHits>=10,"rooftop entry has an upward drivable slope in physics");
   Check(!city.IsRoadEdgeOpen(new Vector2Int(3,0),new Vector2Int(3,1))&&city.IsRoadEdgeOpen(new Vector2Int(2,0),new Vector2Int(2,1)),"GPS avoids internal stunt plaza roads and keeps its perimeter open");
   Check(meshes.Any(m=>m.name=="Visuel Tour_Aerienne_Nord"&&m.GetComponent<Renderer>().bounds.size.y>60),"original sixty-metre aerial towers present near spawn");
   var sourceMeshes=city.cityModel.GetComponentsInChildren<MeshFilter>().ToDictionary(m=>m.name);
   bool aligned=true;foreach(var mesh in meshes.Where(m=>m.name.StartsWith("Visuel Building_")&&m.transform.parent.name.StartsWith("Ilot original"))){var source=sourceMeshes[mesh.name.Substring(7)];var sourceMatrix=city.cityModel.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;if(Quaternion.Angle(mesh.transform.localRotation,sourceMatrix.rotation)>.01f||(mesh.transform.localScale-sourceMatrix.lossyScale).sqrMagnitude>.0001f)aligned=false;}
   Check(aligned,"native facades retain original local orientation and proportions");
   var courseMeshes=meshes.Where(m=>m.transform.parent.name.StartsWith("Parcours des toits - ")).ToArray();
   Check(courseMeshes.Length==city.RooftopPartCount&&courseMeshes.Select(m=>m.name).Distinct().Count()==city.RooftopPartCount,"complete rooftop course has every part exactly once across chunk borders");
   bool linked=true;var reference=courseMeshes[0];var referenceSource=sourceMeshes[reference.name.Substring(7)];var referenceLocal=city.cityModel.transform.InverseTransformPoint(referenceSource.transform.TransformPoint(referenceSource.sharedMesh.bounds.center));
   foreach(var mesh in courseMeshes){var src=sourceMeshes[mesh.name.Substring(7)];var original=city.cityModel.transform.InverseTransformPoint(src.transform.TransformPoint(src.sharedMesh.bounds.center));var expected=reference.transform.parent.rotation*((original-referenceLocal)*(64f/60));var actual=mesh.GetComponent<Renderer>().bounds.center-reference.GetComponent<Renderer>().bounds.center;if(Vector3.Distance(expected,actual)>.05f)linked=false;}
   Check(linked,"rooftop bridges, rings and ramps retain source connection offsets");
   var seenBrands=new HashSet<string>();bool fronts=true;
   for(int i=1;i<=24&&seenBrands.Count<4;i++){
    var coordinate=new Vector2Int(i*8,0);Call(city,"LoadChunk",coordinate);var chunk=city.WorldRoot.Find("Quartier "+coordinate.x+",0");
    var storeMesh=chunk.GetComponentsInChildren<MeshFilter>().First(m=>m.name.EndsWith("_Shop"));seenBrands.Add(storeMesh.name);
    var signMesh=chunk.GetComponentsInChildren<MeshFilter>().First(m=>m.name.EndsWith("_SignText"));var delta=signMesh.GetComponent<Renderer>().bounds.center-storeMesh.GetComponent<Renderer>().bounds.center;
    if(delta.z<4||Mathf.Abs(delta.x)>1)fronts=false;Call(city,"UnloadChunk",coordinate);
   }
   Check(seenBrands.Count==4&&fronts,"all four rare pickup storefronts face the pickup street, including sideways source WINGZ");
   var randomState=UnityEngine.Random.state;city.CoordinateSeed(-50,900);Check(UnityEngine.Random.state.Equals(randomState),"generation hash preserves gameplay randomness");
   Check(city.ChunkAt(city.Origin-new Vector3(.1f,0,.1f))==new Vector2Int(-1,-1),"negative chunk coordinates use floor");
   var districts=new HashSet<ProceduralCity.District>();for(int x=-12;x<12;x++)for(int z=-12;z<12;z++)districts.Add(city.DistrictAt(x,z));Check(districts.Count==5,"all five district types appear");
   var originChunk=city.WorldRoot.Find("Quartier 0,0");string fingerprint=Fingerprint(originChunk);
   Check(city.WorldRoot.GetComponentsInChildren<CroustyShop>().Length>0,"restaurants generated");Check(manager.deliveryPoints.Count>=6,"generated addresses registered");
   Call(manager,"LoadOrders");Check(manager.CroustyLeft==6&&manager.deliveryPoints.Count(p=>p.IsWaiting)==6,"six orders selected on generated map");
   Check(gps.SelectNext(),"GPS selects a generated address");Check(gps.Charges==2,"GPS spends exactly one charge");var target=gps.Target;
   Check(gps.Route.Count>0,"GPS produces street route");
   foreach(var p in gps.Route){var local=p-city.Origin;bool street=Mathf.Abs(local.x/64-Mathf.Round(local.x/64))<.001f||Mathf.Abs(local.z/64-Mathf.Round(local.z/64))<.001f;Check(street,"GPS waypoint lies on connected street grid");}
   Physics.SyncTransforms();
   foreach(var point in manager.deliveryPoints){if(!point.IsWaiting)continue;Check(Physics.Raycast(point.transform.position+Vector3.up*2,Vector3.down,out var hit,4)&&Mathf.Abs(hit.point.y-point.transform.position.y)<.3f,"delivery marker outside building and at ground level");}
   var route=new List<Vector3>();Check(city.TryBuildRoute(city.Origin+new Vector3(5000,2,-8000),target.transform.position,route),"GPS can guide back from unexplored distant city");
   Check(city.TryBuildRoute(city.Origin+new Vector3(192,0,0),city.Origin+new Vector3(192,0,128),route),"GPS finds a detour around the full-size mega ramp");
   Check(route.All(p=>{var q=p-city.Origin;return !(q.x>128.1f&&q.x<255.9f&&q.z>.1f&&q.z<127.9f);}),"GPS detour never crosses the stunt plaza interior");
   manager.player.position=city.Origin+new Vector3(2048,1,2048);
   Drive(city,250);
   Check(city.ChunkAt(manager.player.position)==new Vector2Int(16,16),"streaming follows distant movement");Check(gps.Target==target&&gps.Charges==2,"streaming preserves GPS target and charges");Check(target!=null&&target.gameObject.activeInHierarchy,"waiting delivery chunk remains pinned");
   Check(city.LoadedChunkCount<=75,"loaded chunk count bounded including outstanding orders");
   Check(manager.shop!=null&&manager.shop.isActiveAndEnabled,"a real refill restaurant stays available beyond the visible streaming radius");
   target.MarkPending();Drive(city,1);Check(target.gameObject.activeInHierarchy,"airborne delivery remains pinned");
   foreach(var p in manager.deliveryPoints.ToArray())p.SetOrderActive(false);
   Drive(city,80);Check(city.WorldRoot.Find("Quartier 0,0")==null||!city.WorldRoot.Find("Quartier 0,0").gameObject.activeInHierarchy,"unneeded old neighbourhood unloads");
   Check(manager.deliveryPoints.All(p=>p!=null&&p.isActiveAndEnabled),"unloaded addresses removed from registry");
   manager.player.position=city.Origin+new Vector3(0,1,0);Drive(city,250);
   Check(Fingerprint(city.WorldRoot.Cast<Transform>().First(t=>t.name=="Quartier 0,0"&&t.gameObject.activeInHierarchy))==fingerprint,"revisiting regenerates identical buildings and positions");
   Physics.SyncTransforms();
   for(int i=-1;i<=1;i++){var pos=city.Origin+new Vector3(i*128,2,0);Check(Physics.Raycast(pos,Vector3.down,out var hit,4)&&Mathf.Abs(hit.point.y-city.groundHeight)<.01f,"road surface has no height seam between chunks");}
   Check(FindObjectsByType<CarFollowPoints>().All(c=>c.route!=null&&c.route.gameObject.activeInHierarchy&&!c.IsStopped),"existing traffic rebound to generated roads");
   var store=manager.shop;manager.player.position=store.transform.position+Vector3.forward*9;Check(store.IsInRange(manager.player.position),"restaurant accessible from street");
   Call(manager,"LoadOrders");target=manager.deliveryPoints.First(p=>p.IsWaiting);manager.player.position=target.transform.position;Call(manager,"ThrowCrousty",target);
   Check(target.HasOpenOrder&&!target.IsWaiting,"thrown delivery is pending");
  }catch(Exception e){Fail(e);yield break;}
  // Batch rendering can make a frame longer than the throw. Wait for completion,
  // with a deadline, rather than assuming coroutine order in a single slow frame.
  float deliveryDeadline=Time.realtimeSinceStartup+5;
  while(FindAnyObjectByType<DeliveryManager>().Delivered==0&&Time.realtimeSinceStartup<deliveryDeadline)yield return null;
  try {
   var manager=FindAnyObjectByType<DeliveryManager>();Check(manager.Delivered==1&&manager.CroustyLeft==5,"projectile arrival completes real generated delivery");
   var city=FindAnyObjectByType<ProceduralCity>();
   manager.player.position=city.Origin;Drive(city,250);
   RenderSettings.fog=false;var cameraObject=new GameObject("Preview");var camera=cameraObject.AddComponent<Camera>();camera.transform.position=city.Origin+new Vector3(580,390,-130);camera.transform.LookAt(city.Origin+new Vector3(150,0,160));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.8f,.9f);camera.farClipPlane=1200;
   new GameObject("Sun").AddComponent<Light>().type=LightType.Directional;
   var rt=new RenderTexture(1400,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1400,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1400,900),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"preview.png"),texture.EncodeToPNG());
   camera.transform.position=city.Origin+new Vector3(290,75,-5);camera.transform.LookAt(city.Origin+new Vector3(192,9,64));camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1400,900),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"ramp-preview.png"),texture.EncodeToPNG());
   var police=city.WorldRoot.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="Visuel Police_Station").GetComponent<Renderer>().bounds;
   camera.transform.position=police.center+new Vector3(47,28,55);camera.transform.LookAt(police.center+Vector3.up*2);camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1400,900),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"police-preview.png"),texture.EncodeToPNG());
   camera.transform.position=city.Origin+new Vector3(27,28,-32);camera.transform.LookAt(city.Origin+new Vector3(0,1,0));camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1400,900),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"intersection-preview.png"),texture.EncodeToPNG());
   File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"result.txt"),"PASSED "+count+" checks");Debug.Log("PROCEDURAL CHECKS PASSED: "+count);EditorApplication.Exit(0);
  }catch(Exception e){Fail(e);}
 }
 void Fail(Exception e) {Debug.LogException(e);File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"result.txt"),"FAILED "+e);EditorApplication.Exit(1);}
}


