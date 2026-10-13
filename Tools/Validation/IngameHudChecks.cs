// Copy to Assets of a full isolated project; run IngameHudChecks.Run.
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public sealed class IngameHudChecks : MonoBehaviour
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        new GameObject("Ingame HUD checks").AddComponent<IngameHudChecks>();
        EditorApplication.isPlaying = true;
    }
    private int failures;
    private void Check(bool good, string description) { if (!good) failures++; Debug.Log((good ? "HUD_PASS " : "HUD_FAIL ") + description); }
    private static T Ref<T>(Object owner, string field) where T : Object => new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(4);
        var hud = FindAnyObjectByType<CroustyHUD>();
        var map = FindAnyObjectByType<BusMinimap>();
        var bonus = FindAnyObjectByType<CroustyBonusHUD>();
        var pet = FindAnyObjectByType<BusPetController>();
        var dialogue = FindAnyObjectByType<GerantDialogue>();
        var score = Ref<TMP_Text>(hud, "scoreValue");
        var objective = Ref<TMP_Text>(hud, "objectiveValue");
        var speed = Ref<TMP_Text>(hud, "speedValue");
        var delivery = Ref<TMP_Text>(hud, "deliveryValue");
        Check(hud.enabled && score != null && objective != null && speed != null && delivery != null, "live HUD references");
        Check(objective.text.Contains("Récupère") && delivery.text == "0/" + hud.manager.NextRunOrders, "pickup objective and live orders in French");
        Check(Ref<RectTransform>(map, "maskRect").GetComponent<Mask>() != null, "circular minimap mask");
        var badge = speed.transform.parent.GetComponentInChildren<MinimapSpeedBadge>();
        Check(badge != null && badge.transform.GetSiblingIndex() > Ref<RectTransform>(map, "maskRect").GetSiblingIndex() && badge.transform.GetSiblingIndex() < speed.transform.GetSiblingIndex(), "speed badge drawn over map and below speed text");
        var bonusProps = new SerializedObject(bonus);
        var petImage = bonusProps.FindProperty("pet.artwork").objectReferenceValue as Image;
        var count = bonusProps.FindProperty("pet.countText").objectReferenceValue as TMP_Text;
        Check(petImage.transform.parent.gameObject.activeSelf && count.text == "x3", "single toxic gas card with real charges");
        Check(pet.BonusEquipped && !FindAnyObjectByType<BusGPS>().BonusEquipped && !FindAnyObjectByType<BusFlightController>().BonusEquipped, "only equipped bonus can activate");
        foreach (var other in new[] { "gps", "rice", "flight" })
            Check(!(bonusProps.FindProperty(other + ".artwork").objectReferenceValue as Image).transform.parent.gameObject.activeSelf, "other bonus hidden: " + other);
        Check(Ref<TMP_Text>(dialogue, "authoredMessage").text.Contains("commandes"), "authored French manager dialogue");
        Check(FindObjectsByType<Canvas>().Count(c => c.name == "Canvas Gerant") == 0, "no duplicate runtime dialogue canvas");
        var canvasObjects = FindObjectsByType<Canvas>();
        var camera = Camera.main;
        foreach (var canvas in canvasObjects)
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        hud.showWaypoints = false;
        Capture(camera, "hud-ingame-live", 1920, 1080);

        // Preview the exact mock values, only inside this test process.
        typeof(DeliveryManager).GetField("<Score>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud.manager, 6767);
        typeof(CroustyHUD).GetField("shownScore", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, 6767f);
        var body = hud.manager.player.GetComponentInParent<Rigidbody>();
        var velocity = body.linearVelocity; body.linearVelocity = body.transform.forward * (130f / 3.6f);
        hud.SendMessage("Update");
        Check(score.text == "6,8k" && speed.text.StartsWith("130\n"), "live formatted score and stacked speed");
        var oldMask = camera.cullingMask; var oldClear = camera.clearFlags; var oldColor = camera.backgroundColor;
        camera.cullingMask = 1 << LayerMask.NameToLayer("UI"); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
        foreach (var canvas in canvasObjects) foreach (var transform in canvas.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = LayerMask.NameToLayer("UI");
        Capture(camera, "hud-maquette-1920", 1920, 1080);
        objective.ForceMeshUpdate();
        var message = Ref<TMP_Text>(dialogue, "authoredMessage"); message.ForceMeshUpdate();
        Check(objective.textInfo.characterInfo[objective.text.Length - 1].isVisible, "objective fully readable without clipping");
        Check(message.textInfo.characterInfo[message.text.Length - 1].isVisible, "dialogue fully readable without clipping");
        Capture(camera, "hud-maquette-1280", 1280, 720);
        Capture(camera, "hud-maquette-4x3", 1280, 960);
        camera.cullingMask = oldMask; camera.clearFlags = oldClear; camera.backgroundColor = oldColor; body.linearVelocity = velocity;

        for (int use = 0; use < 3; use++)
        {
            typeof(BusPetController).GetProperty("BoostRemaining").SetValue(pet, 0f);
            Check(pet.TryActivate(), "toxic gas can be consumed: " + use);
            bonus.Refresh();
            Check(count.text == "x" + (2 - use), "charge count follows actual consumption: " + use);
        }
        Check(!petImage.transform.parent.gameObject.activeSelf, "card disappears at zero charges");
        Check(!pet.TryActivate(), "empty gas cannot be used again");
        bonus.SelectBonus(CroustyBonusHUD.SelectedBonus.Flight);
        Check(!pet.BonusEquipped && !FindAnyObjectByType<BusGPS>().BonusEquipped && FindAnyObjectByType<BusFlightController>().BonusEquipped, "selection equips exactly one new bonus");
        Check((bonusProps.FindProperty("flight.artwork").objectReferenceValue as Image).transform.parent.gameObject.activeSelf, "selected bonus can replace gas in same slot");
        Time.timeScale = 0;
        yield return null;
        Check(!Ref<Canvas>(hud, "canvas").enabled && !Ref<Canvas>(dialogue, "authoredCanvas").enabled && !Ref<Canvas>(map, "mapCanvas").enabled, "all HUD hides during pause");
        Debug.Log("HUD_CHECKS_DONE failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void Capture(Camera camera, string name, int width, int height)
    {
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../" + name + ".png"), image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(image);
    }
}
#endif
