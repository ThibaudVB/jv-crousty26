using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class TrafficChecksSetup
{
 public static void Run() {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
  var bus=new GameObject("Bus");bus.AddComponent<BusController>();bus.transform.position=new Vector3(118.546646f,-1.22f,168.1f);
  var manager=new GameObject("DeliveryManager").AddComponent<DeliveryManager>();manager.player=bus.transform;manager.firstRunOrders=6;manager.ordersStep=6;manager.throwDuration=.1f;
  manager.gameObject.AddComponent<BusGPS>().deliveries=manager;
  var city=new GameObject("ProceduralCity").AddComponent<ProceduralCity>();city.cityModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/CroustyCity_PastelPalette_V3_Unity.fbx");city.player=bus.transform;city.deliveries=manager;city.randomSeed=false;city.seed=26011;
  foreach(string arg in System.Environment.GetCommandLineArgs())if(arg.StartsWith("--city-seed="))city.seed=int.Parse(arg.Substring(12));
  new GameObject("Tests").AddComponent<TrafficChecks>();
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Test.unity");
  EditorApplication.EnterPlaymode();
 }
}
