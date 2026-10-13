using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
// Drives the procedural traffic for 3600 frames with a fixed frame step
// and measures lanes, junction rules, spacing, progress, the bus and recycling.
public sealed class TrafficChecks : MonoBehaviour
{
 int count;
 void Check(bool pass,string message) {if(!pass)throw new Exception(message);count++;Debug.Log("TRAFFIC PASS: "+message);}
 static readonly FieldInfo Released=typeof(CityTrafficCar).GetField("released",BindingFlags.Instance|BindingFlags.NonPublic);
 static readonly FieldInfo StoppedAt=typeof(CityTrafficCar).GetField("stoppedAt",BindingFlags.Instance|BindingFlags.NonPublic);
 static string Describe(CityTrafficCar c)=>$"{c.name} pos={c.transform.position:F1} {c.From}->{c.To} turn={c.NextTurn} inJ={c.InJunction} v={c.Speed:F1}";
 static Vector3 Right(Vector2Int h)=>new Vector3(h.y,0,-h.x);
 IEnumerator Start() {
  Time.captureDeltaTime=1f/30f;
  yield return null;
  var city=FindAnyObjectByType<ProceduralCity>();var bus=city.player;
  var busBody=bus.GetComponent<Rigidbody>();if(busBody!=null){busBody.isKinematic=true;busBody.useGravity=false;}
  try {
   Check(city.VehicleModelCount>=2,"vehicle models load from Resources/Crousty/Vehicles");
  } catch(Exception e){Fail(e);yield break;}
  for(int i=0;i<600&&city.Traffic.Count<city.trafficCars;i++)yield return null;
  try {
   Check(city.Traffic.Count==city.trafficCars,"all "+city.trafficCars+" cars spawned on loaded streets ("+city.Traffic.Count+")");
   Check(city.Traffic.All(c=>c.CompareTag("voitures")&&c.GetComponent<BoxCollider>()!=null&&c.GetComponent<Rigidbody>().isKinematic&&c.GetComponentsInChildren<Renderer>().Length>0),"cars are visible, tagged for bus crashes, kinematic with a collider");
   Check(city.Traffic.All(c=>Mathf.Abs(c.GetComponent<BoxCollider>().size.z-city.vehicleLength)<.01f),"bodies are scaled to the configured length");
   Check(city.Traffic.All(c=>Mathf.Abs(c.transform.position.y-city.Origin.y)<.1f),"cars sit on the road surface");
  } catch(Exception e){Fail(e);yield break;}

  var start=city.Traffic.ToDictionary(c=>c,c=>c.transform.position);
  var last=new Dictionary<CityTrafficCar,Vector3>(start);var travelled=city.Traffic.ToDictionary(c=>c,c=>0f);
  var lastHeading=city.Traffic.ToDictionary(c=>c,c=>c.Heading);
  int left=0,right=0,straight=0,laneSamples=0,laneErrors=0,redRuns=0,redCrossings=0,stopRuns=0,stopCrossings=0,overlaps=0,frames=0;float worstLane=0;
  var front=new Dictionary<CityTrafficCar,float>();var wasReleased=new Dictionary<CityTrafficCar,bool>();
  for(int frame=0;frame<3600;frame++)
  {
   yield return new WaitForFixedUpdate();
   yield return null;
   frames++;
   var cars=city.Traffic.Where(c=>c!=null).ToList();
   foreach(var car in cars)
   {
    Vector3 p=car.transform.position;
    if(!last.ContainsKey(car)){last[car]=p;travelled[car]=0;lastHeading[car]=car.Heading;}
    float moved=Vector3.Distance(p,last[car]);if(moved<20f)travelled[car]+=moved;last[car]=p;
    if(car.Heading!=lastHeading[car]){var a=lastHeading[car];var b=car.Heading;if(b==new Vector2Int(a.y,-a.x))right++;else if(b==-new Vector2Int(a.y,-a.x))left++;lastHeading[car]=b;}
    else if(car.InJunction&&car.NextTurn==CityTrafficCar.Turn.Straight)straight++;
    Vector3 node=city.JunctionPosition(car.To);Vector3 h=ProceduralCity.Flat(car.Heading);
    float toNode=Vector3.Dot(node-p,h);
    // On the straight part of a street the car centre is in the right-hand lane.
    if(!car.InJunction&&toNode>ProceduralCity.JunctionEntry+1f&&toNode<64f-ProceduralCity.JunctionEntry-1f)
    {float lateral=Vector3.Dot(p-node,Right(car.Heading));laneSamples++;float error=Mathf.Abs(lateral-ProceduralCity.LaneOffset);worstLane=Mathf.Max(worstLane,error);if(error>.6f)laneErrors++;}
    float bumper=toNode-car.HalfLength;var rule=city.RuleAt(car.To,-car.Heading);
    if(front.TryGetValue(car,out float before)&&before>18f&&bumper<=18f&&!car.InJunction)
    {
     if(rule==ProceduralCity.RoadRule.Stop){stopCrossings++;if((float)StoppedAt.GetValue(car)<0f)stopRuns++;}
    }
    front[car]=bumper;
    // A car commits to the junction (released) only on green or a late amber, never on red.
    bool isReleased=(bool)Released.GetValue(car);
    if(isReleased&&!(wasReleased.TryGetValue(car,out bool was)&&was)&&!car.InJunction&&rule==ProceduralCity.RoadRule.TrafficLight){redCrossings++;if(city.LightAt(car.To,-car.Heading,Time.time)==ProceduralCity.LightAspect.Red)redRuns++;}
    wasReleased[car]=isReleased;
   }
   for(int i=0;i<cars.Count;i++)for(int j=i+1;j<cars.Count;j++){var d=cars[i].transform.position-cars[j].transform.position;d.y=0;if(d.magnitude<2f){overlaps++;if(overlaps<=12)Debug.Log($"TRAFFIC OVERLAP t={Time.time:F1} {Describe(cars[i])} | {Describe(cars[j])}");}}
  }
  try {
   foreach(var pair in travelled)if(pair.Key!=null&&pair.Value<800f)Debug.Log("TRAFFIC SLOW "+pair.Value.ToString("F0")+" m: "+Describe(pair.Key));
   Debug.Log($"TRAFFIC STATS: turns L{left} R{right} straight-frames {straight}, lane samples {laneSamples} errors {laneErrors} worst {worstLane:F2}, red crossings {redCrossings} runs {redRuns}, stop crossings {stopCrossings} runs {stopRuns}, overlap pair-frames {overlaps}/{frames}, travelled {string.Join(",",travelled.Values.Select(v=>v.ToString("F0")))}");
   Check(laneSamples>1000&&laneErrors==0,"cars keep the right-hand lane on every street (worst offset "+worstLane.ToString("F2")+" m)");
   Check(left>0&&right>0&&straight>0,"cars turn left, right and go straight through junctions");
   Check(redCrossings>0&&redRuns==0,"no car commits to a junction on red ("+redCrossings+" signal crossings)");
   Check(stopCrossings==0||stopRuns==0,"every car halts at a STOP before crossing ("+stopCrossings+" crossings)");
   Check(overlaps<frames/50,"cars keep their distance ("+overlaps+" overlapping pair-frames)");
   var distances=travelled.Values.OrderBy(v=>v).ToList();
   Check(distances[distances.Count/2]>400f,"traffic flows: median distance "+distances[distances.Count/2].ToString("F0")+" m over 3600 frames");
  } catch(Exception e){Fail(e);yield break;}

  // The bus parked across a lane holds the car behind it.
  CityTrafficCar follower=null;
  foreach(var car in city.Traffic){Vector3 node=city.JunctionPosition(car.To);if(!car.InJunction&&Vector3.Dot(node-car.transform.position,ProceduralCity.Flat(car.Heading))>45f){follower=car;break;}}
  if(follower==null){var car=city.Traffic[0];car.Place(car.From,car.To,city.LanePoint(car.From,car.Heading,ProceduralCity.JunctionEntry+2f));follower=car;}
  Vector3 blockAt=follower.transform.position+follower.transform.forward*16f;bus.position=blockAt;if(busBody!=null)busBody.position=blockAt;
  for(int i=0;i<150;i++)yield return null;
  try {
   var offset=bus.position-follower.transform.position;offset.y=0;
   Check(follower.Speed<.2f&&offset.magnitude>follower.HalfLength+4f,"a car waits behind the bus instead of driving through it ("+offset.magnitude.ToString("F1")+" m)");
  } catch(Exception e){Fail(e);yield break;}

  // Teleporting the bus far away recycles the cars around it.
  Vector3 far=city.Origin+new Vector3(64*12+20,0,64*9);bus.position=far;if(busBody!=null)busBody.position=far;
  for(int i=0;i<1500;i++)yield return null;
  try {
   float limit=(city.viewDistance+.5f)*ProceduralCity.ChunkSize;
   Check(city.Traffic.Count>=city.trafficCars-1&&city.Traffic.All(c=>{var d=c.transform.position-far;d.y=0;return d.magnitude<=limit+20f;}),"traffic is recycled around the bus after a teleport");
  } catch(Exception e){Fail(e);yield break;}

  // A car destroyed by the bus is replaced.
  var victim=city.Traffic[0];Destroy(victim.gameObject);
  for(int i=0;i<200;i++)yield return null;
  try {Check(city.Traffic.Count==city.trafficCars&&!city.Traffic.Contains(victim),"an exploded car is replaced");} catch(Exception e){Fail(e);yield break;}
  Debug.Log("TRAFFIC CHECKS COMPLETE: "+count+" passed");
  EditorApplication.Exit(0);
 }
 void Fail(Exception e){Debug.LogError("TRAFFIC FAIL after "+count+" passes: "+e.Message);EditorApplication.Exit(1);}
}
