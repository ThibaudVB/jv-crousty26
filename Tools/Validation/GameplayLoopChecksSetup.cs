using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class GameplayLoopChecksSetup
{
 public static void Run() {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
  var bus=new GameObject("Bus");bus.AddComponent<BusController>();bus.transform.position=new Vector3(118.546646f,-1.22f,168.1f);
  var manager=new GameObject("DeliveryManager").AddComponent<DeliveryManager>();manager.player=bus.transform;manager.firstRunOrders=6;manager.ordersStep=6;manager.throwDuration=.1f;
  var gps=manager.gameObject.AddComponent<BusGPS>();gps.deliveries=manager;
  for(int i=0;i<4;i++){var car=new GameObject("Car "+i).AddComponent<CarFollowPoints>();car.transform.position=bus.transform.position;car.debugLogs=false;}
  var city=new GameObject("ProceduralCity").AddComponent<ProceduralCity>();city.cityModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/CroustyCity_PastelPalette_V3_Unity.fbx");city.player=bus.transform;city.deliveries=manager;city.randomSeed=false;city.seed=26011;
  foreach(string arg in System.Environment.GetCommandLineArgs())if(arg.StartsWith("--city-seed="))city.seed=int.Parse(arg.Substring(12));
  var checks=new GameObject("Tests").AddComponent<GameplayLoopChecks>();
  var hud=new GameObject("HUD").AddComponent<CroustyHUD>();hud.manager=manager;
  var canvas=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HudTemplate.prefab"),hud.transform).GetComponent<UnityEngine.Canvas>();
  hud.showWaypoints=false;
  var fields=new UnityEditor.SerializedObject(hud);
  fields.FindProperty("canvas").objectReferenceValue=canvas;
  fields.FindProperty("waypointTemplate").objectReferenceValue=canvas.transform.Find("Mod\u00e8le de rep\u00e8re");
  fields.FindProperty("speedValue").objectReferenceValue=canvas.transform.Find("Compteur de vitesse/Vitesse kmh").GetComponent<TMPro.TMP_Text>();
  fields.FindProperty("deliveryValue").objectReferenceValue=canvas.transform.Find("Carte livraisons/Progression livraisons").GetComponent<TMPro.TMP_Text>();
  fields.FindProperty("objectiveValue").objectReferenceValue=canvas.transform.Find("Carte objectif/Consigne objectif").GetComponent<TMPro.TMP_Text>();
  fields.ApplyModifiedPropertiesWithoutUndo();hud.PrepareUI();checks.canvas=canvas;checks.hud=hud;
  bus.AddComponent<Rigidbody>().isKinematic=true;
  var loss = new GameObject("Game over manager").AddComponent<PoliceGameOverManager>();
  var lossPanel = new GameObject("Death panel",typeof(RectTransform));lossPanel.transform.SetParent(canvas.transform,false);
  var label = new GameObject("Death reason",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TMP_Text>();label.transform.SetParent(lossPanel.transform,false);label.text="LA POLICE VOUS A RATTRAPE !";label.font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Menu/RetroBlendy SDF.asset");
  var lossFields=new SerializedObject(loss);lossFields.FindProperty("gameOverPanel").objectReferenceValue=lossPanel;lossFields.FindProperty("busController").objectReferenceValue=bus.GetComponent<BusController>();lossFields.ApplyModifiedPropertiesWithoutUndo();checks.defeat=loss;checks.defeatPanel=lossPanel;
  var menu = new GameObject("Test menu").AddComponent<CroustyMenu>();checks.menu=menu;
  menu.mainPanel=Panel("Menu main",canvas.transform);menu.optionsPanel=Panel("Options",canvas.transform);
  menu.categoryPanels=new[]{Panel("Controls",menu.optionsPanel.transform),Panel("Audio",menu.optionsPanel.transform)};
  menu.tabs=new CroustyMenuHover[2];menu.tabBackgrounds=new UnityEngine.UI.Graphic[2];menu.tabShadows=new UnityEngine.UI.Graphic[2];menu.tabLabels=new TMPro.TMP_Text[2];
  for(int i=0;i<2;i++){var tab=Panel("Tab"+i,menu.optionsPanel.transform);menu.tabs[i]=tab.AddComponent<CroustyMenuHover>();menu.tabBackgrounds[i]=tab.AddComponent<UnityEngine.UI.Image>();menu.tabLabels[i]=new GameObject("Tab label",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TMP_Text>();menu.tabLabels[i].transform.SetParent(tab.transform,false);}
  menu.sensitivitySlider=new GameObject("Sensitivity",typeof(RectTransform),typeof(UnityEngine.UI.Slider)).GetComponent<UnityEngine.UI.Slider>();menu.sensitivitySlider.minValue=.2f;menu.sensitivitySlider.maxValue=1.5f;
  menu.volumeSlider=new GameObject("Volume",typeof(RectTransform),typeof(UnityEngine.UI.Slider)).GetComponent<UnityEngine.UI.Slider>();
  menu.sensitivityLabel=Text("Sensitivity label");menu.volumeLabel=Text("Volume label");menu.fullscreenLabel=Text("Fullscreen label");
  var cam=new GameObject("Listener");cam.AddComponent<AudioListener>();
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Test.unity");
  EditorApplication.EnterPlaymode();
 }
 static TMPro.TMP_Text Text(string name) {return new GameObject(name,typeof(RectTransform),typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TMP_Text>();}
 static GameObject Panel(string name,Transform parent) {var panel=new GameObject(name,typeof(RectTransform));panel.transform.SetParent(parent,false);panel.transform.localScale=Vector3.one*1.25f;return panel;}
}
