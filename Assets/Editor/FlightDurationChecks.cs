using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FlightDurationChecks
{
    static int frames;
    static FlightDurationChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        SessionState.SetBool("FlightDurationChecks.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool("FlightDurationChecks.Pending", false) || !EditorApplication.isPlaying || ++frames < 20) return;
        SessionState.SetBool("FlightDurationChecks.Pending", false);
        EditorApplication.isPaused = true;
        try { Check(); Debug.Log("FLIGHT_DURATION_CHECKS_PASS"); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); Debug.Log("PASS: " + message); }
    static void Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    static void Check()
    {
        Time.timeScale = 1;
        Physics.simulationMode = SimulationMode.Script;
        var flight = UnityEngine.Object.FindAnyObjectByType<BusFlightController>();
        var rb = flight.GetComponent<Rigidbody>();
        var damage = flight.GetComponent<BusDamage>();
        var hud = flight.GetComponent<BusFlightHUD>();
        flight.GetComponent<PowerUpInventory>().Initialize();
        Set(flight, "city", null);
        rb.position = new Vector3(0, 2000, 0);
        rb.rotation = Quaternion.identity;
        rb.linearVelocity = new Vector3(25, 0, 0);
        flight.flightDuration = 100;
        Assert(flight.AddCharges(1, 1), "Flight charge granted");
        flight.ActivateFlight();
        Assert(flight.RemainingFlightSeconds == 15, "Serialized duration above limit is capped at 15 seconds");
        Call(hud, "Update");
        Assert(hud.timerText.enabled && hud.timerText.text.Contains("15"), "Timer visible at activation");
        Time.timeScale = 0;
        Call(flight, "FixedUpdate");
        Assert(flight.RemainingFlightSeconds == 15, "Pause freezes flight timer");
        Time.timeScale = 1;
        for (int i = 0; i < 375; i++) Call(flight, "FixedUpdate");
        Assert(Mathf.Abs(flight.RemainingFlightFraction - .5f) < .002f, "Half duration leaves half the bar");
        Set(flight, "timeRemaining", Time.fixedDeltaTime * .5f);
        Vector3 momentum = new Vector3(30, 4, 12);
        rb.linearVelocity = momentum;
        Call(flight, "FixedUpdate");
        Assert(flight.IsFalling && flight.RemainingFlightSeconds == 0, "Expiry enters free fall");
        Assert(rb.useGravity && rb.linearVelocity == momentum, "Expiry preserves full momentum and enables gravity");
        Call(flight, "FixedUpdate");
        Assert(rb.linearVelocity == momentum, "Free fall controller does not erase velocity");
        Call(hud, "Update");
        Assert(!hud.timerText.enabled, "Timer hides when flight expires");
        flight.enabled = false;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, 1990, 0);
        floor.transform.localScale = new Vector3(100, 1, 100);
        var box = flight.GetComponent<BoxCollider>();
        foreach (bool upright in new[] { true, false })
        {
            damage.ExtinguishFire();
            rb.rotation = upright ? Quaternion.identity : Quaternion.Euler(0, 0, 180);
            rb.position = new Vector3(0, 2000, 0);
            Physics.SyncTransforms();
            rb.position += Vector3.up * (1990.6f - box.bounds.min.y);
            Physics.SyncTransforms();
            Set(damage, "highestY", rb.position.y + 100);
            Set(damage, "wasGrounded", false);
            Set(damage, "lastHitTime", -999f);
            Call(damage, "FixedUpdate");
            Assert(upright ? damage.HitsTaken == 0 : damage.HitsTaken == 1,
                upright ? "Upright landing after 100m fall does not damage bus" : "Roof-first landing still damages bus");
        }
    }
}
