// Real pickups and activations in an isolated copy of SceneBase.
using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PowerUpIntegrationChecks
{
    const string Key="PowerUpIntegrationChecks";
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static double deadline, ready;
    static int errors, existingHudErrors;
    static BusController bus;
    static PowerUpInventory inventory;
    static BusPetController pet;
    static BusRiceThrower rice;
    static BusFlightController flight;
    static BusGPS gps;
    static DeliveryManager deliveries;
    static CroustyBonusHUD hud;
    static PowerUpIntegrationChecks()
    {
        deadline=EditorApplication.timeSinceStartup+150;
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{
            if(SessionState.GetInt(Key,0)==0)return;
            if(t==LogType.Error&&m=="HUD : assigner les panels et indications dans la scene.")existingHudErrors++;
            else if(t==LogType.Error||t==LogType.Exception)errors++;
        };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();
    }
    static void Check(bool value,string label)
    {if(!value)throw new Exception("POWERUP_FAILED: "+label);Debug.Log("POWERUP_OK: "+label);}
    static object Get(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
    static void Property(object o,string name,object value)=>Set(o,"<"+name+">k__BackingField",value);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
    static void Reset()
    {
        inventory.Initialize();Set(pet,"usesRemaining",0);
        Property(rice,"UsesRemaining",0);Property(flight,"UsesRemaining",0);Property(gps,"Charges",0);
        EnableAll();gps.RebuildNavigation(gps.navigationRoot);
    }
    static void EnableAll(){pet.enabled=rice.enabled=flight.enabled=gps.enabled=true;bus.enabled=true;}
    static void Pickup(PowerUpInventory.PowerUpType type,int expected)
    {
        pet.enabled=type==PowerUpInventory.PowerUpType.Pet;
        rice.enabled=type==PowerUpInventory.PowerUpType.Rice;
        flight.enabled=type==PowerUpInventory.PowerUpType.Flight;
        gps.enabled=type==PowerUpInventory.PowerUpType.GPS;
        var box=new GameObject("Validation pickup").AddComponent<BoxCollider>().gameObject.AddComponent<PowerUpBox>();
        box.respawnDelay=60;
        Call(box,"OnTriggerEnter",bus.GetComponent<BoxCollider>());
        Check(!(bool)Get(box,"available"),"Real box awards "+type);
        int charges=type==PowerUpInventory.PowerUpType.Pet?pet.UsesRemaining:
            type==PowerUpInventory.PowerUpType.Rice?rice.UsesRemaining:
            type==PowerUpInventory.PowerUpType.Flight?flight.UsesRemaining:gps.Charges;
        Check(charges==expected,"Pickup grants "+expected+" uses of "+type);
        UnityEngine.Object.DestroyImmediate(box.gameObject);EnableAll();
    }
    static void HudCount(string kind,int count)
    {
        hud.Refresh();var card=(CroustyBonusHUD.Card)Get(hud,kind);
        Check(card.countText!=null&&card.countText.text=="x"+count,"HUD displays "+kind+" x"+count);
    }
    static void Tick()
    {
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Power-up checks timed out");
            if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1||EditorApplication.timeSinceStartup<ready)return;
            if(stage==1)
            {
                Physics.simulationMode=SimulationMode.Script;
                bus=UnityEngine.Object.FindAnyObjectByType<BusController>();
                inventory=bus.GetComponent<PowerUpInventory>();pet=bus.GetComponent<BusPetController>();
                rice=bus.GetComponent<BusRiceThrower>();flight=bus.GetComponent<BusFlightController>();
                gps=UnityEngine.Object.FindAnyObjectByType<BusGPS>();deliveries=gps.deliveries;
                deliveries.enabled=false;
                foreach(var entry in UnityEngine.Object.FindObjectsByType<CroustyBonusHUD>())
                    if((int)Get(entry,"inventorySlotIndex")==0)hud=entry;
                Check(hud!=null,"Primary authored HUD found");
                Reset();Pickup(PowerUpInventory.PowerUpType.Pet,3);HudCount("pet",3);
                Check(!pet.AddCharges(3,3),"Full PET cannot exceed three uses");
                for(int left=2;left>=0;left--)
                {
                    Check(inventory.TryActivatePrimary(),"PET can activate for use "+(3-left));
                    Check(pet.UsesRemaining==left&&inventory.TotalCharges==left,"PET controller and inventory stay synchronized");
                    if(left>0)HudCount("pet",left);
                }
                Check(!inventory.TryActivatePrimary()&&!pet.TryActivate(),"Fourth PET activation is rejected without negative charges");
                Reset();Pickup(PowerUpInventory.PowerUpType.Rice,3);HudCount("rice",3);
                for(int left=2;left>=0;left--)
                {
                    Check(inventory.TryActivatePrimary(),"RICE can activate for use "+(3-left));
                    Check(rice.UsesRemaining==left&&inventory.TotalCharges==left,"RICE controller and inventory stay synchronized");
                    if(left>0)HudCount("rice",left);
                }
                Check(!inventory.TryActivatePrimary()&&!rice.TryActivate(),"Fourth RICE activation is rejected");
                Reset();Pickup(PowerUpInventory.PowerUpType.Pet,3);
                inventory.TryActivatePrimary();
                Check(pet.AddCharges(3,3)&&pet.UsesRemaining==3&&inventory.GetChargesAt(0)==3&&!inventory.secondary.occupied,
                    "Partially used PET refills the existing slot without duplicates");
                Reset();Pickup(PowerUpInventory.PowerUpType.GPS,1);HudCount("gps",1);
                Check(!gps.AddCharges(3,3)&&!inventory.TryAddPower(PowerUpInventory.PowerUpType.GPS,1),"GPS capacity is one across both slots");
                foreach(var point in deliveries.deliveryPoints)if(point!=null)point.SetOrderActive(false);
                Check(!inventory.TryActivatePrimary()&&gps.Charges==1&&inventory.TotalCharges==1,"GPS keeps its charge if no order is loaded");
                var minimap=deliveries.GetComponent<BusMinimap>();Call(minimap,"UpdateGPS");
                var gpsLabel=(TMP_Text)Get(minimap,"gpsLabel");
                Check(gpsLabel.gameObject.activeSelf&&gpsLabel.text.Contains("commandes"),"GPS explains missing orders on minimap");
                Reset();Pickup(PowerUpInventory.PowerUpType.Pet,3);Pickup(PowerUpInventory.PowerUpType.GPS,1);
                Check(inventory.PrimaryPower==PowerUpInventory.PowerUpType.Pet&&inventory.GetPowerAt(1)==PowerUpInventory.PowerUpType.GPS,
                    "GPS can occupy reserve slot behind PET");
                hud.CycleBonus(1);
                Check(inventory.PrimaryPower==PowerUpInventory.PowerUpType.GPS&&inventory.GetChargesAt(0)==1&&inventory.GetChargesAt(1)==3,
                    "HUD cycling swaps real contents and preserves uses");
                Call(deliveries,"LoadOrders");
                Check(deliveries.RunActive,"Real delivery orders load for GPS activation");
                Check(inventory.TryActivatePrimary(),"GPS activates from reserve after switching: "+gps.Status);
                Check(gps.Target!=null&&gps.Route.Count>=2&&gps.Charges==0,"GPS builds an actual route and consumes exactly one use");
                Check(inventory.PrimaryPower==PowerUpInventory.PowerUpType.Pet&&inventory.GetChargesAt(0)==3,
                    "Used GPS automatically promotes remaining PET");
                Check(!gps.SelectNext()&&gps.Route.Count>=2,"GPS route remains visible after charge consumption");
                var target=gps.Target;target.CompleteDelivery();Call(gps,"OnDelivered",target);
                Check(gps.Target==null&&gps.Route.Count==0,"Completed destination clears GPS route");
                Reset();Pickup(PowerUpInventory.PowerUpType.Flight,1);HudCount("flight",1);
                Check(!flight.AddCharges(3,3),"Flight cannot store more than one use");
                var body=bus.GetComponent<Rigidbody>();body.position+=Vector3.up*100;
                Check(inventory.TryActivatePrimary()&&flight.IsAirborne&&flight.UsesRemaining==0&&!inventory.HasCharge,
                    "Flight consumes one use and starts real flight");
                ready=EditorApplication.timeSinceStartup+.7;SessionState.SetInt(Key,2);return;
            }
            var wings=bus.GetComponent<BusWings>();var root=(Transform)Get(wings,"wingsRoot");
            Check(root.gameObject.activeSelf,"One-use flight still deploys installed wings");
            Check(!inventory.TryActivatePrimary(),"Empty inventory cannot activate again");
            Time.timeScale=0;
            Check(!inventory.SwapSlots()&&!inventory.TryActivatePrimary(),"Pause prevents switching and activation");
            Check(errors==0&&existingHudErrors<=1,"No new Unity runtime errors");
            Debug.Log("POWERUP_CHECKS_DONE failures=0");SessionState.SetInt(Key,0);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(1);}
    }
}
