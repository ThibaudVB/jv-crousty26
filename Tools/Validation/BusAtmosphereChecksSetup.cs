using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BusAtmosphereChecksSetup
{
 public static void Run() {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
  var bus=new GameObject("Bus");bus.AddComponent<BusController>();bus.transform.position=new Vector3(118.546646f,-1.22f,168.1f);
  var manager=new GameObject("DeliveryManager").AddComponent<DeliveryManager>();manager.player=bus.transform;manager.firstRunOrders=6;manager.ordersStep=6;manager.throwDuration=.1f;
  var gps=manager.gameObject.AddComponent<BusGPS>();gps.deliveries=manager;
  for(int i=0;i<4;i++){var car=new GameObject("Car "+i).AddComponent<CarFollowPoints>();car.transform.position=bus.transform.position;car.debugLogs=false;}
  var city=new GameObject("ProceduralCity").AddComponent<ProceduralCity>();city.cityModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/CroustyCity_PastelPalette_V3_Unity.fbx");city.player=bus.transform;city.deliveries=manager;city.randomSeed=false;city.seed=26011;
  foreach(string arg in System.Environment.GetCommandLineArgs())if(arg.StartsWith("--city-seed="))city.seed=int.Parse(arg.Substring(12));
  var vehicle=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CroustyBus.prefab"));
  vehicle.transform.position=bus.transform.position+new Vector3(0,0,-5);
  foreach(var body in vehicle.GetComponentsInChildren<Rigidbody>())body.isKinematic=true;
  var checks=new GameObject("Tests").AddComponent<BusAtmosphereChecks>();
  checks.bus=vehicle.AddComponent<BusNightLights>();
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Test.unity");
  EditorApplication.EnterPlaymode();
 }
}
