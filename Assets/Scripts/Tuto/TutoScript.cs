using System.Collections;
using UnityEngine;

public sealed class TutoScript : MonoBehaviour
{
    [SerializeField] private Canvas deliveryCanvas;
    [SerializeField] private GameObject tutorialCanvas;
    [SerializeField] private GameObject hudMaquette;
    [SerializeField] private GameObject atouts;
    [SerializeField] private GameObject atoutsAlternate;
    [SerializeField] private Canvas dialogueCanvas;
    [SerializeField] private GameObject lbDialogue;
    [SerializeField] private GerantDialogue managerDialogue;
    [SerializeField] private AudioClip introVoice;
    [SerializeField] private AudioClip afterFirstPickupVoice;
    [SerializeField] private AudioClip afterFirstDeliveryVoice;
    [SerializeField] private AudioClip repairGarageVoice;
    [SerializeField] private AudioClip beforePowerBoxPickupVoice;
    [SerializeField] private AudioClip afterPowerBoxPickupVoice;
    [SerializeField] private AudioClip tutorialMusic;
    [SerializeField, Range(0f, 1f)] private float tutorialMusicVolume = 1f;
    [SerializeField] private AudioClip policeImpactSound;
    [SerializeField, Range(0f, 1f)] private float policeImpactVolume = 1f;
    [SerializeField] private GameObject tutorialLootboxPrefab;
    [SerializeField, Min(1f)] private float dialogueMaquetteScale = 1.5f;

    private AudioSource voiceSource;
    private AudioSource musicSource;
    private DeliveryManager deliveryManager;
    private PoliceChaseManager policeManager;
    private BusDamage busDamage;
    private Rigidbody introBusBody;
    private BusController introBusController;
    private BusFlightController introFlightController;
    private RigidbodyConstraints introBusConstraints;
    private Transform[] dialogueContent;
    private Vector3[] dialogueContentBaseScales;
    private bool introControllerWasEnabled;
    private bool introFlightWasEnabled;
    private bool firstPickupDialogueShown;
    private bool firstDeliveryDialogueShown;
    private bool tutorialRewardSpawned;
    private bool tutorialPowerBoxCollected;
    private bool tutorialDamageStateCaptured;
    private bool damageWasEnabledBeforeTutorial;

    private const string IntroMessage =
        "Alright rookie, eyes on the screen! See that yellow spot? Get your butt over there and pick up the order. Chop chop, time is money!";
    private const string AfterFirstPickupMessage =
        "Good, now go deliver it to the green mark. And drive like you actually want to keep this job will ya?";
    private const string AfterFirstDeliveryMessage =
        "Boom! Nice move kid! But don’t pop the champagne yet, they're still behind us! Grab the next order and keep pushing; it’s only getting faster from here!";
    private const string RepairGarageMessage =
        "Those cops wrecked the truck! Set the GPS route to the nearest garage and get it repaired!";
    private const string BeforePowerBoxPickupMessage =
        "Hey kid, pick up that crate and let's see what's inside.";
    private const string AfterPowerBoxPickupMessage =
        "Hey quick, press the {0} and use your fart! Yeah, you heard me, let it rip and lose those badge-wearing party poopers, and get the job done!";
    private const float CameraStartDelay = 2f;
    private const float CameraLookDuration = 3f;
    private const float CameraTurnDuration = 0.4f;

    private void Awake()
    {
        if (PlayerPrefs.GetInt(CroustyMenuTutoButton.TutorialEnabledKey, 0) != 1)
            return;

        if (managerDialogue != null)
            managerDialogue.SetAutomaticDialogueEnabled(false);
        else
            Debug.LogError("TutoScript : GerantDialogue est nécessaire pour couper ses répliques automatiques pendant le tutoriel.", this);

        policeManager = FindAnyObjectByType<PoliceChaseManager>();
        if (policeManager == null)
        {
            Debug.LogError("TutoScript : PoliceChaseManager est nécessaire pour désactiver les apparitions pendant le chargement.", this);
            return;
        }

        policeManager.SetSpawningEnabled(false);
    }

    private IEnumerator Start()
    {
        bool tutorialEnabled =
            PlayerPrefs.GetInt(CroustyMenuTutoButton.TutorialEnabledKey, 0) == 1;
        deliveryManager = GetComponent<DeliveryManager>();
        if (policeManager == null)
            policeManager = FindAnyObjectByType<PoliceChaseManager>();

        if (deliveryCanvas == null || tutorialCanvas == null ||
            hudMaquette == null || atouts == null || atoutsAlternate == null ||
            dialogueCanvas == null || lbDialogue == null || managerDialogue == null ||
            tutorialEnabled && (deliveryManager == null || policeManager == null))
        {
            Debug.LogError("TutoScript : assigner les Canvas, les HUD, LB dialogue et le dialogue du gérant; vérifier aussi les managers du tutoriel.", this);
            enabled = false;
            yield break;
        }

        if (tutorialEnabled)
        {
            busDamage = deliveryManager.player != null
                ? deliveryManager.player.GetComponentInParent<BusDamage>()
                : null;
            if (busDamage == null)
            {
                Debug.LogError("TutoScript : BusDamage est requis pour détecter la réparation du camion.", this);
                enabled = false;
                yield break;
            }

            damageWasEnabledBeforeTutorial = busDamage.DamageEnabled;
            tutorialDamageStateCaptured = true;
            busDamage.SetDamageEnabled(false);
            policeManager.SetSpawningEnabled(false);
            if (!LockBusForIntro())
            {
                RestoreTutorialDamageState();
                enabled = false;
                yield break;
            }

            yield return new WaitForSecondsRealtime(5f);
        }

        if (tutorialEnabled)
        {
            deliveryManager.ConfigureTutorialOrderCounts(1, 3);
            deliveryManager.RunStarted += OnRunStarted;
            deliveryManager.DeliveryCompleted += OnDeliveryCompleted;
            busDamage.Repaired += OnBusRepaired;
        }

        deliveryCanvas.enabled = !tutorialEnabled;
        tutorialCanvas.SetActive(tutorialEnabled);
        hudMaquette.SetActive(!tutorialEnabled);
        atouts.SetActive(!tutorialEnabled);
        atoutsAlternate.SetActive(!tutorialEnabled);

        if (tutorialEnabled)
        {
            CacheDialogueContentScales();
            SetDialogueMaquetteScale(dialogueMaquetteScale);
            dialogueCanvas.enabled = true;
            lbDialogue.SetActive(false);
            voiceSource = GetComponent<AudioSource>();
            if (voiceSource == null)
                voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f;
            if (tutorialMusic != null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.clip = tutorialMusic;
                musicSource.volume = tutorialMusicVolume;
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f;
                musicSource.Play();
            }
            StartCoroutine(ShowIntroAfterDialogueStarts());
            StartCoroutine(LookAtShopLight());
        }
    }

    private bool LockBusForIntro()
    {
        introBusBody = deliveryManager.player != null
            ? deliveryManager.player.GetComponentInParent<Rigidbody>()
            : null;
        introBusController = introBusBody != null
            ? introBusBody.GetComponent<BusController>()
            : null;
        introFlightController = introBusBody != null
            ? introBusBody.GetComponent<BusFlightController>()
            : null;
        if (introBusBody == null || introBusController == null)
        {
            Debug.LogError("TutoScript : impossible de bloquer le bus pendant le dialogue d'introduction.", this);
            return false;
        }

        introBusConstraints = introBusBody.constraints;
        introControllerWasEnabled = introBusController.enabled;
        introFlightWasEnabled = introFlightController != null && introFlightController.enabled;
        introBusBody.linearVelocity = Vector3.zero;
        introBusBody.angularVelocity = Vector3.zero;
        introBusBody.constraints = RigidbodyConstraints.FreezeAll;
        introBusController.enabled = false;
        if (introFlightController != null)
            introFlightController.enabled = false;
        return true;
    }

    private void UnlockBusAfterIntro()
    {
        if (introBusBody == null || introBusController == null)
            return;

        introBusBody.constraints = introBusConstraints;
        introBusBody.linearVelocity = Vector3.zero;
        introBusBody.angularVelocity = Vector3.zero;
        introBusController.enabled = introControllerWasEnabled;
        if (introFlightController != null)
            introFlightController.enabled = introFlightWasEnabled;
    }

    private void RestoreTutorialDamageState()
    {
        if (!tutorialDamageStateCaptured || busDamage == null)
            return;

        busDamage.SetDamageEnabled(damageWasEnabledBeforeTutorial);
        tutorialDamageStateCaptured = false;
    }

    private void OnDestroy()
    {
        UnlockBusAfterIntro();
        if (musicSource != null)
            musicSource.Stop();
        if (deliveryManager != null)
        {
            deliveryManager.RunStarted -= OnRunStarted;
            deliveryManager.DeliveryCompleted -= OnDeliveryCompleted;
        }
        if (busDamage != null)
        {
            busDamage.Repaired -= OnBusRepaired;
            RestoreTutorialDamageState();
        }
    }

    private void OnRunStarted()
    {
        if (tutorialPowerBoxCollected)
        {
            if (musicSource != null)
                musicSource.Stop();
            if (policeManager != null)
                policeManager.SetSpawningEnabled(true);
            managerDialogue.SetAutomaticDialogueEnabled(true);
            SetDialogueMaquetteScale(1f);
            lbDialogue.SetActive(true);
        }

        if (firstPickupDialogueShown)
            return;

        firstPickupDialogueShown = true;
        StartCoroutine(ShowAfterFirstPickupDialogue());
    }

    private void CacheDialogueContentScales()
    {
        int childCount = dialogueCanvas.transform.childCount;
        dialogueContent = new Transform[childCount];
        dialogueContentBaseScales = new Vector3[childCount];
        for (int i = 0; i < childCount; i++)
        {
            Transform child = dialogueCanvas.transform.GetChild(i);
            dialogueContent[i] = child;
            dialogueContentBaseScales[i] = child.localScale;
        }
    }

    private void SetDialogueMaquetteScale(float scale)
    {
        if (dialogueContent == null || dialogueContentBaseScales == null)
            return;

        for (int i = 0; i < dialogueContent.Length; i++)
            if (dialogueContent[i] != null)
                dialogueContent[i].localScale = dialogueContentBaseScales[i] * scale;
    }

    private void OnDeliveryCompleted(DeliveryPoint point)
    {
        if (!firstPickupDialogueShown || firstDeliveryDialogueShown)
            return;

        firstDeliveryDialogueShown = true;
        StartCoroutine(PlayFirstDeliveryCutscene());
    }

    private System.Collections.IEnumerator PlayFirstDeliveryCutscene()
    {
        yield return null;
        managerDialogue.SetMessage(AfterFirstDeliveryMessage);
        if (afterFirstDeliveryVoice != null)
            voiceSource.PlayOneShot(afterFirstDeliveryVoice);

        Rigidbody busBody = deliveryManager.player != null
            ? deliveryManager.player.GetComponentInParent<Rigidbody>()
            : null;
        BusController busController = deliveryManager.player != null
            ? deliveryManager.player.GetComponentInParent<BusController>()
            : null;
        BusFlightController flightController = busBody != null
            ? busBody.GetComponent<BusFlightController>()
            : null;

        if (busBody == null || busController == null || policeManager == null)
        {
            Debug.LogError("TutoScript : impossible de lancer la cinématique; références du bus ou de la police manquantes.", this);
            yield break;
        }

        RigidbodyConstraints originalConstraints = busBody.constraints;
        bool controllerWasEnabled = busController.enabled;
        bool flightWasEnabled = flightController != null && flightController.enabled;
        busBody.linearVelocity = Vector3.zero;
        busBody.angularVelocity = Vector3.zero;
        busBody.constraints = RigidbodyConstraints.FreezeAll;
        busController.enabled = false;
        if (flightController != null)
            flightController.enabled = false;

        Vector3 forward = busBody.rotation * Vector3.forward;
        Vector3 startPosition = busBody.position - forward * 28f;
        Quaternion policeRotation = Quaternion.LookRotation(forward, Vector3.up);
        PoliceCarAI policeCar = policeManager.CreateTutorialPoliceCar(startPosition, policeRotation);

        if (policeCar == null)
        {
            RestoreBusControl(busBody, busController, flightController, originalConstraints,
                controllerWasEnabled, flightWasEnabled);
            yield break;
        }

        Camera mainCamera = Camera.main;
        BusCameraFollow cameraFollow = mainCamera != null
            ? mainCamera.GetComponent<BusCameraFollow>()
            : null;
        if (mainCamera == null || cameraFollow == null)
        {
            Debug.LogError("TutoScript : impossible de cadrer la voiture de police sans la caméra du bus.", this);
            mainCamera = null;
        }

        bool cameraFollowWasEnabled = cameraFollow != null && cameraFollow.enabled;
        Vector3 cameraStartPosition = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        Quaternion cameraStartRotation = mainCamera != null ? mainCamera.transform.rotation : Quaternion.identity;
        if (cameraFollow != null)
            cameraFollow.enabled = false;

        Vector3 impactPosition = busBody.position -
            forward * (policeManager.BusHalfLength + policeCar.HalfLength - 0.15f);
        Rigidbody policeBody = policeCar.GetComponent<Rigidbody>();
        Vector3 carStart = policeBody.position;
        float chargeDuration = 1.35f;
        float elapsed = 0f;
        while (elapsed < chargeDuration)
        {
            elapsed += Time.fixedDeltaTime;
            policeBody.MovePosition(Vector3.Lerp(carStart, impactPosition,
                Mathf.Clamp01(elapsed / chargeDuration)));
            AimCameraAtPolice(mainCamera, policeCar.transform);
            yield return new WaitForFixedUpdate();
        }

        policeBody.MovePosition(impactPosition);
        AimCameraAtPolice(mainCamera, policeCar.transform);
        yield return new WaitForFixedUpdate();

        BusDamage busDamage = busBody.GetComponent<BusDamage>();
        if (busDamage != null)
        {
            if (policeImpactSound != null)
                AudioSource.PlayClipAtPoint(policeImpactSound, busBody.position, policeImpactVolume);
            busDamage.SetDamageEnabled(true);
            busDamage.ApplyTutorialImpact();
        }
        else
            Debug.LogError("TutoScript : BusDamage est nécessaire pour l'impact de la cinématique.", this);
        deliveryManager.SetTutorialPickupMarkerHidden(true);

        policeManager.RemoveTutorialPoliceCar(policeCar);
        if (mainCamera != null)
        {
            yield return ReturnCameraToStart(mainCamera.transform, cameraStartPosition,
                cameraStartRotation, cameraFollow, cameraFollowWasEnabled);
        }
        RestoreBusControl(busBody, busController, flightController, originalConstraints,
            controllerWasEnabled, flightWasEnabled);

        BusGPS gps = FindAnyObjectByType<BusGPS>();
        if (gps == null || !gps.SelectNearestGarageRoute())
            Debug.LogError("TutoScript : impossible de calculer un itinéraire vers le garage le plus proche.", this);

        yield return null;
        managerDialogue.SetMessage(RepairGarageMessage);
        if (repairGarageVoice != null)
        {
            voiceSource.Stop();
            voiceSource.PlayOneShot(repairGarageVoice);
        }
    }

    private static void AimCameraAtPolice(Camera camera, Transform police)
    {
        if (camera == null || police == null)
            return;

        Vector3 lookDirection = police.position - camera.transform.position;
        if (lookDirection.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, targetRotation,
            1f - Mathf.Exp(-8f * Time.deltaTime));
    }

    private static System.Collections.IEnumerator ReturnCameraToStart(Transform camera,
        Vector3 startPosition, Quaternion startRotation, BusCameraFollow cameraFollow,
        bool cameraFollowWasEnabled)
    {
        const float returnDuration = 0.4f;
        Vector3 currentPosition = camera.position;
        Quaternion currentRotation = camera.rotation;
        float elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / returnDuration);
            camera.SetPositionAndRotation(
                Vector3.Lerp(currentPosition, startPosition, progress),
                Quaternion.Slerp(currentRotation, startRotation, progress));
            yield return null;
        }

        camera.SetPositionAndRotation(startPosition, startRotation);
        if (cameraFollow != null)
            cameraFollow.enabled = cameraFollowWasEnabled;
    }

    private static void RestoreBusControl(Rigidbody busBody, BusController busController,
        BusFlightController flightController, RigidbodyConstraints constraints,
        bool controllerWasEnabled, bool flightWasEnabled)
    {
        busBody.constraints = constraints;
        busBody.linearVelocity = Vector3.zero;
        busBody.angularVelocity = Vector3.zero;
        busController.enabled = controllerWasEnabled;
        if (flightController != null)
            flightController.enabled = flightWasEnabled;
    }

    private void OnBusRepaired()
    {
        if (tutorialRewardSpawned)
            return;
        tutorialRewardSpawned = true;

        BusGPS gps = FindAnyObjectByType<BusGPS>();
        if (gps == null)
            Debug.LogError("TutoScript : impossible d'arrêter l'indication GPS des garages.", this);
        else
            gps.DisableGarageGuidance();

        hudMaquette.SetActive(true);
        atouts.SetActive(true);
        atoutsAlternate.SetActive(true);

        StartCoroutine(SpawnRepairRewardSequence());
    }

    private System.Collections.IEnumerator SpawnRepairRewardSequence()
    {
        yield return new WaitForSeconds(3f);

        Rigidbody busBody = deliveryManager.player != null
            ? deliveryManager.player.GetComponentInParent<Rigidbody>()
            : null;
        BusController busController = busBody != null
            ? busBody.GetComponent<BusController>()
            : null;
        BusFlightController flightController = busBody != null
            ? busBody.GetComponent<BusFlightController>()
            : null;
        if (busBody == null || busController == null)
        {
            Debug.LogError("TutoScript : impossible d'arrêter le bus avant l'apparition de la récompense.", this);
            yield break;
        }

        RigidbodyConstraints originalConstraints = busBody.constraints;
        bool controllerWasEnabled = busController.enabled;
        bool flightWasEnabled = flightController != null && flightController.enabled;
        busBody.linearVelocity = Vector3.zero;
        busBody.angularVelocity = Vector3.zero;
        busBody.constraints = RigidbodyConstraints.FreezeAll;
        busController.enabled = false;
        if (flightController != null)
            flightController.enabled = false;

        GameObject box = SpawnGuaranteedPetBox(busBody);
        box.GetComponent<PowerUpBox>().PickedUp += OnTutorialPowerBoxPickedUp;
        managerDialogue.SetMessage(BeforePowerBoxPickupMessage);
        if (beforePowerBoxPickupVoice != null)
        {
            voiceSource.Stop();
            voiceSource.PlayOneShot(beforePowerBoxPickupVoice);
        }
        yield return ShowLootboxWithCamera(busBody, box.transform);

        busBody.constraints = originalConstraints;
        busBody.linearVelocity = Vector3.zero;
        busBody.angularVelocity = Vector3.zero;
        busController.enabled = controllerWasEnabled;
        if (flightController != null)
            flightController.enabled = flightWasEnabled;
    }

    private GameObject SpawnGuaranteedPetBox(Rigidbody busBody)
    {
        Vector3 busForward = Vector3.ProjectOnPlane(busBody.transform.forward, Vector3.up).normalized;
        GetBusFront(busBody, busForward, out Vector3 frontCenter, out float groundHeight,
            out float busHalfLength);
        Vector3 landingPosition = frontCenter + busForward * (busHalfLength + 3f);
        landingPosition.y = groundHeight + 0.6f;
        Vector3 spawnPosition = busBody.position + Vector3.up * 10f;
        GameObject box = tutorialLootboxPrefab != null
            ? Instantiate(tutorialLootboxPrefab, spawnPosition, Quaternion.identity)
            : new GameObject("Lootbox Tutoriel - PET", typeof(SphereCollider), typeof(PowerUpBox));
        box.name = "Lootbox Tutoriel - PET";
        box.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);

        PowerUpBox powerUpBox = box.GetComponent<PowerUpBox>();
        if (powerUpBox == null)
            powerUpBox = box.AddComponent<PowerUpBox>();
        Collider trigger = box.GetComponent<Collider>();
        trigger.isTrigger = true;
        if (trigger is SphereCollider sphereTrigger)
            sphereTrigger.radius = 1.5f;
        powerUpBox.guaranteedPetDrop = true;
        powerUpBox.destroyAfterPickup = true;

        if (tutorialLootboxPrefab == null)
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Caisse";
            crate.transform.SetParent(box.transform, false);
            crate.transform.localScale = new Vector3(1.4f, 1.2f, 1.4f);
            Destroy(crate.GetComponent<Collider>());
            SetLootboxColor(crate.GetComponent<Renderer>(), new Color(1f, 0.68f, 0.08f));

            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = "Bande PET";
            band.transform.SetParent(crate.transform, false);
            band.transform.localPosition = Vector3.zero;
            band.transform.localScale = new Vector3(0.24f, 1.02f, 1.02f);
            Destroy(band.GetComponent<Collider>());
            SetLootboxColor(band.GetComponent<Renderer>(), new Color(0.18f, 0.78f, 0.28f));
        }
        powerUpBox.BeginTutorialDrop(landingPosition, 3f);
        return box;
    }

    private void OnTutorialPowerBoxPickedUp()
    {
        tutorialPowerBoxCollected = true;
        string inputButton = CroustyInput.UsesController ? "X button (console)" : "E key (PC)";
        managerDialogue.SetMessage(string.Format(AfterPowerBoxPickupMessage, inputButton));
        if (afterPowerBoxPickupVoice != null)
        {
            voiceSource.Stop();
            voiceSource.PlayOneShot(afterPowerBoxPickupVoice);
        }
        if (deliveryManager != null)
            deliveryManager.SetTutorialPickupMarkerHidden(false);
    }

    private System.Collections.IEnumerator ShowLootboxWithCamera(Rigidbody busBody, Transform lootbox)
    {
        Camera mainCamera = Camera.main;
        BusCameraFollow cameraFollow = mainCamera != null
            ? mainCamera.GetComponent<BusCameraFollow>()
            : null;
        if (mainCamera == null || cameraFollow == null)
        {
            Debug.LogError("TutoScript : impossible de montrer la lootbox sans la caméra du bus.", this);
            yield break;
        }

        bool cameraFollowWasEnabled = cameraFollow.enabled;
        Vector3 startingPosition = mainCamera.transform.position;
        Quaternion startingRotation = mainCamera.transform.rotation;
        cameraFollow.enabled = false;

        Vector3 busForward = Vector3.ProjectOnPlane(busBody.transform.forward, Vector3.up).normalized;
        GetBusFront(busBody, busForward, out Vector3 frontCenter, out _, out float busHalfLength);
        Vector3 cameraPosition = frontCenter + busForward * (busHalfLength + 2f) +
            Vector3.up * 3f;
        Quaternion cameraRotation = Quaternion.LookRotation(
            lootbox.position + Vector3.up * 0.3f - cameraPosition, Vector3.up);
        const float transitionDuration = 0.5f;
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            mainCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startingPosition, cameraPosition, progress),
                Quaternion.Slerp(startingRotation, cameraRotation, progress));
            yield return null;
        }

        mainCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        yield return new WaitForSeconds(2f);

        elapsed = 0f;
        Vector3 displayPosition = mainCamera.transform.position;
        Quaternion displayRotation = mainCamera.transform.rotation;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            mainCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(displayPosition, startingPosition, progress),
                Quaternion.Slerp(displayRotation, startingRotation, progress));
            yield return null;
        }

        mainCamera.transform.SetPositionAndRotation(startingPosition, startingRotation);
        cameraFollow.enabled = cameraFollowWasEnabled;
    }

    private static void GetBusFront(Rigidbody busBody, Vector3 busForward, out Vector3 frontCenter,
        out float groundHeight, out float halfLength)
    {
        BoxCollider busCollider = busBody.GetComponent<BoxCollider>();
        if (busCollider == null)
        {
            frontCenter = busBody.position;
            groundHeight = busBody.position.y - 0.6f;
            halfLength = 5f;
            return;
        }

        frontCenter = busCollider.transform.TransformPoint(busCollider.center);
        groundHeight = busCollider.bounds.min.y;

        Vector3 halfSize = busCollider.size * 0.5f;
        Transform colliderTransform = busCollider.transform;
        halfLength =
            Mathf.Abs(Vector3.Dot(busForward,
                colliderTransform.TransformVector(Vector3.right * halfSize.x))) +
            Mathf.Abs(Vector3.Dot(busForward,
                colliderTransform.TransformVector(Vector3.up * halfSize.y))) +
            Mathf.Abs(Vector3.Dot(busForward,
                colliderTransform.TransformVector(Vector3.forward * halfSize.z)));
    }

    private static void SetLootboxColor(Renderer renderer, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("TutoScript : impossible de trouver un shader pour la lootbox.");
            return;
        }

        Material material = new Material(shader) { color = color };
        renderer.material = material;
    }

    private System.Collections.IEnumerator LookAtShopLight()
    {
        yield return new WaitForSeconds(CameraStartDelay);

        DeliveryManager deliveryManager = GetComponent<DeliveryManager>();
        Transform lightTarget = deliveryManager != null && deliveryManager.shop != null &&
            deliveryManager.shop.marker != null
            ? deliveryManager.shop.marker.transform.Find("Lumiere")
            : null;
        Camera mainCamera = Camera.main;
        BusCameraFollow cameraFollow = mainCamera != null
            ? mainCamera.GetComponent<BusCameraFollow>()
            : null;

        if (lightTarget == null || mainCamera == null || cameraFollow == null)
        {
            Debug.LogError("TutoScript : impossible de trouver la caméra du bus ou le Lumiere du marqueur jaune.", this);
            yield break;
        }

        bool cameraFollowWasEnabled = cameraFollow.enabled;
        cameraFollow.enabled = false;

        Quaternion startingRotation = mainCamera.transform.rotation;
        Vector3 lookDirection = lightTarget.position - mainCamera.transform.position;
        if (lookDirection.sqrMagnitude > 0f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(lookDirection);
            float turnElapsed = 0f;
            while (turnElapsed < CameraTurnDuration)
            {
                turnElapsed += Time.deltaTime;
                mainCamera.transform.rotation = Quaternion.Slerp(
                    startingRotation, lookRotation, turnElapsed / CameraTurnDuration);
                yield return null;
            }

            mainCamera.transform.rotation = lookRotation;
            yield return new WaitForSeconds(CameraLookDuration - CameraTurnDuration);
        }
        else
        {
            yield return new WaitForSeconds(CameraLookDuration);
        }

        cameraFollow.enabled = cameraFollowWasEnabled;
    }

    private IEnumerator ShowIntroAfterDialogueStarts()
    {
        yield return null;
        managerDialogue.SetMessage(IntroMessage);
        if (introVoice != null)
        {
            voiceSource.PlayOneShot(introVoice);
            yield return new WaitForSecondsRealtime(introVoice.length);
        }
        UnlockBusAfterIntro();
    }

    private System.Collections.IEnumerator ShowAfterFirstPickupDialogue()
    {
        yield return null;
        managerDialogue.SetMessage(AfterFirstPickupMessage);
        if (afterFirstPickupVoice != null)
            voiceSource.PlayOneShot(afterFirstPickupVoice);
    }

}
