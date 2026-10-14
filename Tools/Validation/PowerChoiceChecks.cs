// Copy to Assets in an isolated project. Run PowerChoiceChecks.Run without -quit.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Power = PowerUpInventory.PowerUpType;

public class PowerChoiceChecks : MonoBehaviour
{
    private int failures;
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        new GameObject("Power choice checks").AddComponent<PowerChoiceChecks>();
        EditorApplication.isPlaying = true;
    }
    private void Check(bool value, string label)
    {
        if (!value) failures++;
        Debug.Log((value ? "POWER_PASS " : "POWER_FAIL ") + label);
    }
    private static void Set(object target, string name, object value)
    {
        var type = target.GetType();
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? type.GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(target, value);
    }
    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Complete(DeliveryManager manager, PowerUnlockProgression progression, int size, int loaded)
    {
        Set(manager, "OrdersTotal", size); Set(manager, "Delivered", size); Set(manager, "CroustyLeft", 0);
        Set(manager, "HasStarted", true); Set(manager, "RunActive", false); Set(manager, "runsLoaded", loaded);
        Call(progression, "OnRunCompleted");
    }
    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(5);
        var p = FindAnyObjectByType<PowerUnlockProgression>();
        var manager = p.deliveries;
        var bus = p.GetComponent<BusController>();
        var body = bus.GetComponent<Rigidbody>(); body.isKinematic = true;
        Check(p != null && !p.choicePanel.activeSelf, "serialized panel initially hidden");
        Check(p.IsUnlocked(Power.Pet) && !p.IsUnlocked(Power.GPS) && !p.IsUnlocked(Power.Rice) && !p.IsUnlocked(Power.Flight), "PET-only initial pool");
        Check(p.GetOffers(3).Count == 0 && p.GetOffers(5).SequenceEqual(new[] { Power.GPS, Power.Rice }), "first choice after the five-order run, not tutorial");
        Check(!p.Purchase(0), "purchase rejected outside modal");
        CheckBoxes(p, bus, false);
        Set(manager, "Score", 10000);
        Complete(manager, p, 5, 2);
        yield return null; yield return null;
        Check(Time.timeScale == 0 && p.choicePanel.activeSelf && p.blurredWorld.texture != null, "paused modal with captured blurred world");
        Check(p.choices.All(c => c.interactable), "both first offers affordable");
        ExecuteEvents.Execute(p.choices[1].gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
        Check(EventSystem.current.currentSelectedGameObject == p.choices[1].gameObject, "mouse hover selects the corresponding card");
        p.choices[0].Select();
        Capture("power-choice-1920", 1920, 1080);
        Capture("power-choice-4x3", 1280, 960);
        int score = manager.Score, money = manager.Money;
        ExecuteEvents.Execute(p.choices[0].gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        Check(p.IsUnlocked(Power.GPS), "UI submit buys GPS");
        Check(manager.Money == money - p.gpsPrice && manager.Score == score, "wallet debited without reducing leaderboard score");
        Check(!p.Purchase(0) && Time.timeScale == 1 && !p.choicePanel.activeSelf, "double submit rejected and pause restored");
        yield return WaitForRun(manager);
        Check(manager.RunActive && manager.OrdersTotal == 10, "next run starts directly after purchase");
        Check(p.GetOffers(5).Count == 0 && p.GetOffers(10).SequenceEqual(new[] { Power.Rice, Power.Flight }), "second choice at 10: remaining basic power versus flight");
        CheckBoxes(p, bus, true);
        Complete(manager, p, 10, 3);
        yield return null; yield return null;
        Check(p.Purchase(0), "buy rice at second milestone");
        yield return WaitForRun(manager);
        Check(p.GetOffers(10).Count == 0 && p.GetOffers(15).SequenceEqual(new[] { Power.Flight }), "last choice at 15 is flight on standard path");
        Complete(manager, p, 15, 4);
        yield return null; yield return null;
        Check(p.Purchase(0), "buy flight at final milestone");
        yield return WaitForRun(manager);
        Check(p.GetOffers(25).Count == 0 && p.IsUnlocked(Power.Flight), "no repeated unlock once all powers bought");

        // Alternate branch: buying flight at 10 leaves the basic power at 15.
        var unlocked = (HashSet<Power>)typeof(PowerUnlockProgression).GetField("unlocked", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(p);
        unlocked.Clear(); unlocked.Add(Power.Pet); unlocked.Add(Power.Rice);
        Set(p, "completedChoices", 1); Set(manager, "Score", 20000);
        Complete(manager, p, 10, 3);
        yield return null; yield return null;
        Check(p.Purchase(1), "flight can be bought early when affordable");
        yield return WaitForRun(manager);
        Check(p.GetOffers(15).SequenceEqual(new[] { Power.GPS }), "early flight leaves GPS at final milestone");

        Set(manager, "moneySpent", manager.Score);
        Complete(manager, p, 15, 4);
        yield return null; yield return null;
        Check(!p.Purchase(0) && p.continueButton.gameObject.activeSelf, "insufficient funds do not debit or trap player");
        p.continueButton.onClick.Invoke();
        yield return WaitForRun(manager);
        Check(!PowerUnlockProgression.ChoiceOpen && p.GetOffers(25).Contains(Power.GPS), "unaffordable choice remains available next run");
        Debug.Log("POWER_CHECKS_DONE failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
    private IEnumerator WaitForRun(DeliveryManager manager)
    {
        float until = Time.realtimeSinceStartup + 40;
        while (!manager.RunActive && Time.realtimeSinceStartup < until) yield return null;
    }
    private void CheckBoxes(PowerUnlockProgression p, BusController bus, bool gpsUnlocked)
    {
        var inventory = bus.GetComponent<PowerUpInventory>();
        var pet = bus.GetComponent<BusPetController>();
        var gps = FindAnyObjectByType<BusGPS>();
        var rice = bus.GetComponent<BusRiceThrower>();
        var flight = bus.GetComponent<BusFlightController>();
        bool onlyUnlocked = true;
        var collider = bus.GetComponent<Collider>() ?? bus.GetComponentInChildren<Collider>();
        for (int i = 0; i < 20; i++)
        {
            inventory.Initialize(); Set(pet, "usesRemaining", 0); Set(gps, "Charges", 0);
            Set(rice, "UsesRemaining", 0); Set(flight, "UsesRemaining", 0);
            var box = new GameObject("Test mystery box", typeof(BoxCollider), typeof(PowerUpBox)).GetComponent<PowerUpBox>();
            Call(box, "OnTriggerEnter", collider);
            onlyUnlocked &= inventory.PrimaryPower == Power.Pet || gpsUnlocked && inventory.PrimaryPower == Power.GPS;
            Destroy(box.gameObject);
        }
        Check(onlyUnlocked, gpsUnlocked ? "boxes never award locked rice/flight" : "starting boxes only award PET");
    }
    private static void Capture(string name, int width, int height)
    {
        var camera = Camera.main;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../" + name + ".png"), image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        target.Release(); Destroy(target); Destroy(image);
        foreach (var canvas in canvases) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }
}
#endif
