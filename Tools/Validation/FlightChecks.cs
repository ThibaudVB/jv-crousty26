using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FlightChecks
{
    static int frames;
    static int checks;
    static FlightChecks() { EditorApplication.update += Update; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        SessionState.SetBool("FlightChecks.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Update()
    {
        if (!SessionState.GetBool("FlightChecks.Pending", false) || !EditorApplication.isPlaying) return;
        if (++frames < 15) return;
        SessionState.SetBool("FlightChecks.Pending", false);
        EditorApplication.isPaused = true;
        try { Check(); Debug.Log("FLIGHT CHECKS PASSED: " + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Assert(bool value, string label) { if (!value) throw new Exception(label); checks++; Debug.Log("PASS: " + label); }
    static void Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);
    static object Get(object o, string name) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    static void Check()
    {
        Time.timeScale = 1;
        Physics.simulationMode = SimulationMode.Script;
        var flight = UnityEngine.Object.FindAnyObjectByType<BusFlightController>();
        Assert(flight != null, "SceneBase has flight controller");
        int impactEvents = 0;
        float impactStrength = 0;
        flight.Landed += strength => { impactEvents++; impactStrength = strength; };
        var camera = UnityEngine.Object.FindAnyObjectByType<BusCameraFollow>();
        var body = flight.GetComponent<Rigidbody>();
        var drive = flight.GetComponent<BusController>();
        var rig = flight.GetComponent<BusWheelRig>();
        var grounding = flight.GetComponent<SceneBaseGrounding>();
        var wheels = flight.GetComponentsInChildren<WheelCollider>();
        var police = UnityEngine.Object.FindAnyObjectByType<PoliceChaseManager>();
        var zone = flight.GetComponentInChildren<PoliceCatchZone>();
        var constraints = body.constraints;
        var collision = body.collisionDetectionMode;
        bool gravity = body.useGravity;
        Assert(wheels.Length == 4, "Four runtime wheel colliders");
        Set(drive, "currentSteering", 1f);
        Set(rig, "progressiveSteering", 1f);
        body.position = new Vector3(0, 1000, 0);
        body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        zone.AdvanceCapture(true, 2f);
        Call(flight, "ActivateFlight");
        Assert(flight.CurrentMode == BusFlightController.FlightMode.Flying, "Activation gives immediate flight control");
        Assert(!drive.enabled && !rig.enabled && !body.useGravity, "Exclusive airborne control");
        foreach (var w in wheels) Assert(!w.enabled && w.motorTorque == 0 && w.brakeTorque == 0, "Wheel disabled and torque reset");
        Assert((float)Get(drive, "currentSteering") == 0 && rig.SteeringAmount == 0, "Drift/input reset");
        Call(police, "FixedUpdate");
        Assert(police.ActivePolice == null && zone.CatchProgress01 == 0, "Police removed and capture reset");
        float remaining = flight.RemainingFlightSeconds;
        Call(flight, "ActivateFlight");
        Assert(flight.RemainingFlightSeconds == remaining, "Activation cannot restart airborne timer");
        int ticks = 0;
        Assert(Mathf.Abs(flight.RemainingFlightSeconds - 15f) < .01f, "Full 15 seconds of controllable flight");
        Assert(body.linearVelocity.y >= flight.launchUpSpeed, "Single upward launch impulse");
        Set(flight, "turnInput", 1f);
        float initialYaw = (float)Get(flight, "yaw");
        Call(flight, "FixedUpdate");
        Assert((float)Get(flight, "yaw") > initialYaw, "Steering responds on first flight tick");
        Set(flight, "turnInput", 0f);
        Set(flight, "boostInput", true);
        float boostBefore = (float)Get(flight, "boostRemaining");
        for (int i = 0; i < 10; i++) Call(flight, "FixedUpdate");
        Assert((float)Get(flight, "boostRemaining") < boostBefore, "Boost consumes its budget");
        Set(flight, "boostInput", false);
        while (!flight.IsFalling && ticks++ < 760) Call(flight, "FixedUpdate");
        Assert(flight.IsFalling && flight.RemainingFlightSeconds == 0, "Expiry enters fall");
        Assert(body.useGravity && !drive.enabled && !rig.enabled, "Gravity enabled, ground drive still suspended");
        var fallEffect = flight.GetComponent<BusFallSpeedEffect>();
        Assert(fallEffect != null, "Fall speed effect automatically attached");
        var flightVelocity = body.linearVelocity;
        body.linearVelocity = new Vector3(60, 0, 0);
        for (int i = 0; i < 60; i++)
            fallEffect.GetType().GetMethod("UpdateEffect", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fallEffect, new object[] { 0.02f });
        Assert(fallEffect.Intensity < .001f, "Horizontal speed alone gives no vertical streaks");
        body.linearVelocity = Vector3.down * 10;
        for (int i = 0; i < 60; i++)
            fallEffect.GetType().GetMethod("UpdateEffect", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fallEffect, new object[] { 0.02f });
        float mildFall = fallEffect.Intensity;
        Assert(mildFall > 0 && mildFall < .5f, "Moderate descent gives restrained streaks");
        body.linearVelocity = Vector3.down * 40;
        for (int i = 0; i < 60; i++)
            fallEffect.GetType().GetMethod("UpdateEffect", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fallEffect, new object[] { 0.02f });
        Assert(fallEffect.Intensity > .9f && fallEffect.Intensity > mildFall, "Streaks intensify with real downward speed: " + fallEffect.Intensity + ", mild=" + mildFall + ", dt=" + Time.deltaTime + ", vy=" + body.linearVelocity.y);
        Assert(body.linearVelocity == Vector3.down * 40, "Fall effect never changes physics velocity");
        var fallCanvas = (Canvas)Get(fallEffect, "canvas");
        Assert(fallCanvas.enabled && fallCanvas.GetComponentsInChildren<UnityEngine.UI.Image>().Length == 16, "Bounded set of vertical streaks visible");
        Time.timeScale = 0;
        float frozen = fallEffect.Intensity;
        Call(fallEffect, "Update");
        Assert(fallEffect.Intensity == frozen, "Fall animation respects pause");
        Time.timeScale = 1;
        body.linearVelocity = flightVelocity;
        foreach (var w in wheels) Assert(w.enabled, "Suspension restored for physical impact");
        body.position = new Vector3(0, 1000, 0);
        body.rotation = Quaternion.Euler(10, 20, 25);
        body.linearVelocity = new Vector3(40, -20, 0);
        body.angularVelocity = new Vector3(.1f, .2f, .3f);
        Physics.SyncTransforms();
        var before = body.linearVelocity;
        var rotation = body.rotation;
        Call(flight, "FixedUpdate");
        Assert(body.linearVelocity == before && body.rotation == rotation, "Falling does not cap velocity or level pose");
        Call(grounding, "FixedUpdate");
        Physics.Simulate(Time.fixedDeltaTime);
        Assert(Mathf.Abs(body.linearVelocity.y - before.y - Physics.gravity.y * Time.fixedDeltaTime) < .02f, "Natural gravity without extra terrain force");
        Call(police, "FixedUpdate");
        Assert(police.ActivePolice == null, "Police absent during fall");
        // A real physical impact, including a roof-first arrival.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, 990, 0);
        floor.transform.localScale = new Vector3(1000, 1, 1000);
        body.position = new Vector3(0, 1000, 0);
        body.rotation = Quaternion.Euler(0, 0, 180);
        body.linearVelocity = Vector3.down * 5;
        body.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
        for (int i = 0; i < 200 && flight.IsAirborne; i++) { Call(flight, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime); }
        Assert(!flight.IsAirborne && drive.enabled && rig.enabled, "Real roof contact restores ground controls");
        Call(fallEffect, "Update");
        Assert(fallEffect.Intensity == 0 && !fallCanvas.enabled, "Vertical streaks stop on contact");
        Assert(impactEvents == 1 && impactStrength > 0 && impactStrength <= 1, "One scaled impact event per landing");
        int dust = 0;
        foreach (var ps in flight.GetComponentsInChildren<ParticleSystem>()) dust += ps.particleCount;
        Assert(dust > 0 && dust <= 128, "Impact emits bounded dust particles");
        Assert((float)Get(camera, "landingShakeRemaining") > 0, "Impact starts camera shake");
        for (int i = 0; i < 3; i++) Physics.Simulate(Time.fixedDeltaTime);
        Assert(impactEvents == 1, "Repeated contact does not retrigger impact");
        var cameraPosition = camera.transform.position;
        var cameraRotation = camera.transform.rotation;
        camera.GetType().GetMethod("ApplyLandingShake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(camera, new object[] { 0.02f });
        Assert(camera.transform.position != cameraPosition, "Shake offsets rendered camera");
        Call(camera, "RemoveLandingShake");
        Assert(Vector3.Distance(camera.transform.position, cameraPosition) < 0.00001f && Quaternion.Angle(camera.transform.rotation, cameraRotation) < 0.01f, "Shake removal restores exact base pose");
        for (int i = 0; i < 30; i++)
        {
            camera.GetType().GetMethod("ApplyLandingShake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(camera, new object[] { 0.02f });
            Call(camera, "RemoveLandingShake");
        }
        Assert((float)Get(camera, "landingShakeRemaining") == 0 && camera.transform.position == cameraPosition, "Shake expires without positional drift");
        Assert(body.useGravity == gravity && body.constraints == constraints && body.collisionDetectionMode == collision, "Original Rigidbody settings restored");
        Assert(Vector3.Dot(body.rotation * Vector3.up, Vector3.up) < 0, "Impact does not auto-right the bus");
        Call(police, "FixedUpdate");
        Assert(police.ActivePolice == null, "Post-impact police grace preserved");
        // Repeat on the wheels and verify police actually resumes after its grace.
        body.position = new Vector3(0, 1000, 0);
        body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        Call(flight, "ActivateFlight");
        Call(flight, "StartFalling");
        Physics.SyncTransforms();
        for (int i = 0; i < 200 && flight.IsAirborne; i++) { Call(flight, "FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime); }
        Assert(!flight.IsAirborne && drive.enabled && rig.enabled, "Upright fall restores ground controls");
        Assert(rig.IsGrounded, "Wheel suspension catches the upright bus");
        body.linearVelocity = Vector3.zero;
        for (int i = 0; i < 170; i++) Call(police, "FixedUpdate");
        Assert(police.ActivePolice != null, "Police can respawn after impact and grace");
        var label = new GameObject("Flight HUD check", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        var hud = label.AddComponent<BusFlightHUD>();
        hud.flightController = flight;
        hud.timerText = label.GetComponent<TMPro.TMP_Text>();
        Call(hud, "Update");
        Assert(label.activeSelf && !hud.timerText.enabled, "HUD on its text object stays active on ground");
        // A second activation and disabling midflight restore per-wheel state.
        wheels[0].enabled = false;
        Call(flight, "ActivateFlight");
        Assert(flight.IsAirborne, "Bonus reusable after impact");
        Call(hud, "Update");
        Assert(hud.timerText.enabled && hud.timerText.text.Contains("15"), "HUD reappears on activation");
        flight.enabled = false;
        Assert(!flight.IsAirborne && drive.enabled && rig.enabled && !wheels[0].enabled && wheels[1].enabled, "Disable restores exact original component states");
        Assert(body.constraints == constraints && body.useGravity == gravity, "Disable restores Rigidbody");
        var pet = flight.GetComponent<BusPetController>();
        Assert(pet != null && pet.UsesRemaining == 2, "PET begins with exactly two charges");
        Time.timeScale = 0;
        Assert(!pet.TryActivate() && pet.UsesRemaining == 2, "Pause cannot consume PET");
        Time.timeScale = 1;
        Set(police, "distance", 8f);
        police.ActivePolice.SetFollowingDistance(8f, 0f);
        police.ActivePolice.UpdateFollowingPose(.02f);
        zone.AdvanceCapture(true, 1f);
        float baseLimit = drive.maxSpeedKmh;
        Assert(pet.TryActivate() && pet.UsesRemaining == 1, "PET consumes one charge");
        Assert(police.IsBlinded && police.BlindRemaining <= 5f && zone.CatchProgress01 == 0, "PET blinds nearby rear police and resets capture");
        Assert(pet.IsBoosting && drive.EffectiveMaxSpeedKmh > baseLimit && drive.maxSpeedKmh == baseLimit, "PET raises effective speed without mutating tuning");
        Assert(Mathf.Approximately(drive.EffectiveMaxSpeedKmh, baseLimit * 1.85f) &&
            Mathf.Approximately(drive.PetAccelerationMultiplier, 1.8f), "Stronger PET speed and acceleration multipliers");
        var smoke = flight.GetComponent<SceneBaseDriftSmoke>();
        var cloud = (ParticleSystem)Get(smoke, "petParticles");
        var particles = new ParticleSystem.Particle[128];
        int count = cloud.GetParticles(particles);
        Assert(count == 96 && cloud.main.maxParticles == 128, "Detailed PET burst has bounded dedicated particle budget");
        float small = float.MaxValue, large = 0f;
        bool darkCore = false, olive = false;
        for (int i = 0; i < count; i++)
        {
            small = Mathf.Min(small, particles[i].startSize);
            large = Mathf.Max(large, particles[i].startSize);
            Color color = particles[i].startColor;
            darkCore |= color.g < .35f;
            olive |= color.r > .4f && color.g < .55f;
        }
        Assert(small < .6f && large > 4f && darkCore && olive, "PET has dark core, olive shades and varied detail sizes");
        Assert(cloud.GetComponent<ParticleSystemRenderer>().sharedMaterial != smoke.smokeMaterial,
            "PET shading does not change drift material");
        Assert(!pet.TryActivate() && pet.UsesRemaining == 1, "PET cannot stack while boost active");
        float gapBefore = police.PoliceDistance;
        Call(police, "FixedUpdate");
        Assert(police.PoliceDistance > gapBefore && zone.CatchProgress01 == 0, "Blinded police retreat without capturing");
        for (int i = 0; i < 260; i++) { Call(pet, "FixedUpdate"); Call(police, "FixedUpdate"); }
        Assert(!pet.IsBoosting && !police.IsBlinded && drive.EffectiveMaxSpeedKmh == baseLimit && drive.PetAccelerationMultiplier == 1f, "Boost and blindness expire and restore baseline");
        Assert(pet.TryActivate() && pet.UsesRemaining == 0, "Second PET consumes final charge");
        for (int i = 0; i < 160; i++) Call(pet, "FixedUpdate");
        Assert(!pet.TryActivate() && pet.UsesRemaining == 0, "No third PET use");
    }
}
