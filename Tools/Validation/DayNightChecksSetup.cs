using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class DayNightChecksSetup
{
 public static void Run() {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
  var bus=new GameObject("Bus");bus.AddComponent<BusController>();bus.transform.position=new Vector3(118.546646f,-1.22f,168.1f);
  var manager=new GameObject("DeliveryManager").AddComponent<DeliveryManager>();manager.player=bus.transform;manager.ordersPerRun=6;manager.throwDuration=.1f;
  var gps=manager.gameObject.AddComponent<BusGPS>();gps.deliveries=manager;
  for(int i=0;i<4;i++){var car=new GameObject("Car "+i).AddComponent<CarFollowPoints>();car.transform.position=bus.transform.position;car.debugLogs=false;}
  var city=new GameObject("ProceduralCity").AddComponent<ProceduralCity>();city.cityModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/CroustyCity_PastelPalette_V3_Unity.fbx");city.player=bus.transform;city.deliveries=manager;city.randomSeed=false;city.seed=26011;
  foreach(string arg in System.Environment.GetCommandLineArgs())if(arg.StartsWith("--city-seed="))city.seed=int.Parse(arg.Substring(12));
  var checks=new GameObject("Tests").AddComponent<DayNightChecks>();
  var hud=new GameObject("HUD").AddComponent<CroustyHUD>();hud.manager=manager;
  var canvas=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HudTemplate.prefab"),hud.transform).GetComponent<UnityEngine.Canvas>();
  hud.showWaypoints=false;
  var fields=new UnityEditor.SerializedObject(hud);
  fields.FindProperty("canvas").objectReferenceValue=canvas;
  fields.FindProperty("waypointTemplate").objectReferenceValue=canvas.transform.Find("Mod\u00e8le de rep\u00e8re");
  fields.FindProperty("speedValue").objectReferenceValue=canvas.transform.Find("Compteur de vitesse/Vitesse kmh").GetComponent<TMPro.TMP_Text>();
  fields.FindProperty("deliveryValue").objectReferenceValue=canvas.transform.Find("Carte livraisons/Progression livraisons").GetComponent<TMPro.TMP_Text>();
  fields.FindProperty("objectiveValue").objectReferenceValue=canvas.transform.Find("Carte objectif/Consigne objectif").GetComponent<TMPro.TMP_Text>();
  fields.ApplyModifiedPropertiesWithoutUndo();hud.PrepareUI();checks.previewCanvas=canvas;checks.previewHud=hud;
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Test.unity");
  EditorApplication.EnterPlaymode();
 }
}
