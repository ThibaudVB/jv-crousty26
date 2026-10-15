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
        CheckGodMode();
        CheckPickupAndRing();
        Physics.simulationMode = SimulationMode.Script;
        var flight = UnityEngine.Object.FindAnyObjectByType<BusFlightController>();
        var rb = flight.GetComponent<Rigidbody>();
        CheckWingsAndStreaming(flight, rb);
        var damage = flight.GetComponent<BusDamage>();
        var hud = flight.GetComponent<BusFlightHUD>();
        flight.GetComponent<PowerUpInventory>().Initialize();
        Set(flight, "city", null);
        rb.position = new Vector3(0, 2000, 0);
        rb.rotation = Quaternion.identity;
        rb.linearVelocity = new Vector3(25, 0, 0);
        rb.linearDamping = 2f;
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
        CheckSimulatedMomentum(flight, rb, momentum);
        Call(hud, "Update");
        Assert(!hud.timerText.enabled, "Timer hides when flight expires");
        flight.enabled = false;
        Assert(rb.linearDamping == 2f, "Original road damping restored after flight");
        flight.enabled = true;
        Assert(flight.AddCharges(1, 1), "Manual flight charge granted");
        flight.ActivateFlight();
        rb.linearVelocity = momentum;
        Assert(flight.StopFlight() && flight.IsFalling && rb.linearVelocity == momentum,
            "Manual cancellation preserves inertia just like timer expiry");
        CheckFallingTricks(flight, rb);
        CheckSimulatedMomentum(flight, rb, momentum);
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
    static void CheckWingsAndStreaming(BusFlightController flight, Rigidbody rb)
    {
        var rig = flight.GetComponent<BusWheelRig>();
        var wings = flight.GetComponent<BusWings>();
        var root = (Transform)typeof(BusWings).GetField("wingsRoot",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wings);
        Assert(root.parent == rig.VisualPivot, "Wings follow the animated body pivot");
        Assert(root.GetComponentsInChildren<Collider>(true).Length == 0, "Wings remain visual-only");
        Quaternion oldVisualRotation = rig.VisualPivot.localRotation;
        Vector3 localAttachment = root.localPosition;
        try
        {
            foreach (float angle in new[] { 90f, 180f, 270f })
            {
                rig.VisualPivot.localRotation = Quaternion.Euler(angle, 20f, 15f);
                Assert(Vector3.Distance(root.position, rig.VisualPivot.TransformPoint(localAttachment)) < .001f,
                    "Wing attachment follows somersault at " + angle + " degrees");
                Assert(Quaternion.Angle(root.rotation, rig.VisualPivot.rotation) < .1f,
                    "Wing axes remain aligned with animated body");
            }
        }
        finally { rig.VisualPivot.localRotation = oldVisualRotation; }
        Assert(rb.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic,
            "Swept building collisions are enabled on the ground");
        var city = UnityEngine.Object.FindAnyObjectByType<ProceduralCity>();
        Vector3 oldPosition = rb.position, oldVelocity = rb.linearVelocity;
        try
        {
            // Cross fresh district boundaries, including negative coordinates and
            // a fast diagonal flight. No dependence on the current delivery count.
            foreach (float direction in new[] { 1f, -1f })
            {
                rb.position = city.Origin + new Vector3(direction * 4096f, 200f, direction * 4096f);
                rb.linearVelocity = new Vector3(direction * 60f, -30f, direction * 60f);
                Physics.SyncTransforms();
                Call(city, "FixedUpdate");
                Assert(city.IsAreaReady(rb.position) && city.IsAreaReady(rb.position + rb.linearVelocity * Time.fixedDeltaTime),
                    "Current and imminent districts have their collisions before physics");
                int chunks = city.LoadedChunkCount;
                Call(city, "FixedUpdate");
                Assert(city.LoadedChunkCount == chunks, "Collision protection does not rebuild existing districts");
            }
        }
        finally { rb.position = oldPosition; rb.linearVelocity = oldVelocity; Physics.SyncTransforms(); }
    }
    static void CheckFallingTricks(BusFlightController flight, Rigidbody rb)
    {
        var tricks = flight.GetComponent<BusAirTricks>();
        var rig = flight.GetComponent<BusWheelRig>();
        float minimum = tricks.minAirTime;
        Vector3 velocity = rb.linearVelocity;
        Quaternion rotation = rb.rotation;
        try
        {
            tricks.minAirTime = 1000f; // Keep this deterministic without a physical keyboard.
            Set(tricks, "airTime", 1f);
            Set(tricks, "pitch", 90f);
            Call(tricks, "Update");
            Call(tricks, "LateUpdate");
            Assert(Quaternion.Angle(rig.trickRotation, Quaternion.Euler(90f, 0f, 0f)) < .1f,
                "Falling retains the existing somersault animation");
            var pivot = (Transform)typeof(BusWheelRig).GetField("visualPivot",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rig);
            Assert(Quaternion.Angle(pivot.localRotation, Quaternion.identity) > 80f,
                "Somersault is rendered even while driving rig is disabled");
            Assert(rb.linearVelocity == velocity && rb.rotation == rotation && !rig.enabled,
                "Falling tricks preserve physical momentum and orientation");
            Set(tricks, "pitch", 360f);
            int previous = tricks.TricksLanded;
            Call(tricks, "Land");
            Assert(tricks.TricksLanded == previous + 1, "Completed post-flight flip uses existing landing reward");
        }
        finally { tricks.minAirTime = minimum; tricks.ResetTrick(); }
    }
    static void CheckGodMode()
    {
        bool hadName = PlayerPrefs.HasKey(CroustyPlayerProfile.NameKey);
        string savedName = PlayerPrefs.GetString(CroustyPlayerProfile.NameKey, "");
        var progression = UnityEngine.Object.FindAnyObjectByType<PowerUnlockProgression>();
        var damage = UnityEngine.Object.FindAnyObjectByType<BusDamage>();
        var zone = UnityEngine.Object.FindAnyObjectByType<PoliceCatchZone>();
        var gameOver = UnityEngine.Object.FindAnyObjectByType<PoliceGameOverManager>();
        try
        {
            CroustyPlayerProfile.Save("CathyLaVodka");
            Assert(CroustyPlayerProfile.IsGodMode, "Exact reserved name enables GOD");
            foreach (PowerUpInventory.PowerUpType power in Enum.GetValues(typeof(PowerUpInventory.PowerUpType)))
                Assert(progression.IsUnlocked(power), "GOD unlocks " + power);
            int hits = damage.HitsTaken;
            Set(damage, "lastHitTime", -999f);
            damage.TakePoliceProjectileHit(damage.transform.position);
            Assert(damage.HitsTaken == hits, "GOD ignores police damage");
            zone.SetProgress(1f);
            gameOver.TriggerPoliceGameOver();
            Assert(!zone.IsCaught && !gameOver.IsGameOver, "GOD cannot be captured or receive police game over");
            foreach (string other in new[] { "cathylavodka", "CATHYLAVODKA", "CathyLaVodka2",
                "xCathyLaVodka", " CathyLaVodka", "CathyLaVodka ", "CathyLa\u0001Vodka",
                "CathyLaVоdka", "CathyLaVodka\u200b", "Alice", "", null })
            {
                CroustyPlayerProfile.Save("CathyLaVodka");
                CroustyPlayerProfile.Save(other);
                Assert(!CroustyPlayerProfile.IsGodMode, "Other input revokes GOD: " + (other ?? "null"));
                Assert(!progression.IsUnlocked(PowerUpInventory.PowerUpType.Flight),
                    "GOD unlocks do not leak into another profile");
            }
        }
        finally
        {
            if (hadName) PlayerPrefs.SetString(CroustyPlayerProfile.NameKey, savedName);
            else PlayerPrefs.DeleteKey(CroustyPlayerProfile.NameKey);
            PlayerPrefs.Save();
        }
    }
    static void CheckSimulatedMomentum(BusFlightController flight, Rigidbody rb, Vector3 momentum)
    {
        Assert(!flight.GetComponent<BusController>().enabled && !flight.GetComponent<BusWheelRig>().enabled,
            "Road controls remain disabled during fall");
        foreach (var wheel in flight.GetComponentsInChildren<WheelCollider>(true))
            Assert(!wheel.enabled, "Tire physics remains disabled until landing");
        Vector3 start = rb.position;
        for (int i = 0; i < 50; i++)
        {
            Call(flight, "FixedUpdate");
            Physics.Simulate(Time.fixedDeltaTime);
        }
        Assert(flight.IsFalling, "Bus remains airborne during simulated fall");
        Assert(Vector3.Distance(Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up),
            Vector3.ProjectOnPlane(momentum, Vector3.up)) < .1f,
            "Horizontal momentum survives one second of actual physics");
        Assert(Vector3.ProjectOnPlane(rb.position - start, Vector3.up).magnitude > 30f,
            "Bus continues travelling forward after flight ends");
    }
    static void CheckPickupAndRing()
    {
        var bus = UnityEngine.Object.FindAnyObjectByType<BusController>();
        var inventory = PowerUpInventory.EnsureFor(bus.gameObject);
        var choices = new System.Collections.Generic.List<CroustyBonusHUD.SelectedBonus> {
            CroustyBonusHUD.SelectedBonus.Pet, CroustyBonusHUD.SelectedBonus.GPS,
            CroustyBonusHUD.SelectedBonus.Rice, CroustyBonusHUD.SelectedBonus.Flight };
        for (int round = 0; round < 50; round++)
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 4; i++) seen.Add(inventory.ChooseBoxReward(choices));
            Assert(seen.Count == 4, "All four available rewards appear once per randomized bag");
        }
        inventory.Initialize();
        inventory.TryAddPower(PowerUpInventory.PowerUpType.Pet, 3);
        inventory.TryAddPower(PowerUpInventory.PowerUpType.GPS, 1);
        var crateObject = new GameObject("Full inventory pickup check");
        var trigger = crateObject.AddComponent<BoxCollider>();
        var crate = crateObject.AddComponent<PowerUpBox>();
        crate.respawnDelay = 18f;
        typeof(PowerUpBox).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(crate, new object[] { bus.GetComponentInChildren<BoxCollider>() });
        Assert(!trigger.enabled && inventory.TotalCharges == 4,
            "Full inventory consumes the box without changing its contents");
        UnityEngine.Object.Destroy(crateObject);
        inventory.Initialize();

        var ringObject = new GameObject("Ring aperture check");
        ringObject.AddComponent<BoxCollider>();
        var ring = ringObject.AddComponent<FireRingSoundZone>();
        ring.apertureExtents = new Vector3(5, 5, 1);
        Assert(ring.InsideOpening(Vector3.zero), "Ring center accepts a crossing");
        Assert(!ring.InsideOpening(new Vector3(0, -6, 0)), "Passing beneath the ring is rejected");
        Assert(!ring.InsideOpening(new Vector3(4, 4, 0)), "Trigger corners outside the round hole are rejected");
        UnityEngine.Object.Destroy(ringObject);
    }

}
