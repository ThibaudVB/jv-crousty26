using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class PowerUpBox : MonoBehaviour
{
    [Header("Respawn")]
    [Min(0f)] public float respawnDelay = 8f;

    [Header("Visuel")]
    [Tooltip("Objet qui disparait quand la box est prise. Si vide, utilise tous les Renderers enfants.")]
    public GameObject visual;
    public float rotationSpeed = 90f;
    public float bobAmplitude = .25f;
    public float bobSpeed = 2f;

    [Header("Feedback (optionnel)")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = .8f;

    private Collider trigger;
    private Renderer[] renderers;
    private Vector3 startLocalPosition;
    private bool available = true;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
        if (visual == null) renderers = GetComponentsInChildren<Renderer>();
        startLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        if (!available) return;
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);
        var p = startLocalPosition;
        p.y += Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.localPosition = p;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!available) return;

        var bus = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<BusController>()
            : other.GetComponentInParent<BusController>();
        if (bus == null) return;

        PowerUpInventory.EnsureFor(bus.gameObject);
        var progression = bus.GetComponent<PowerUnlockProgression>();

        var candidates = new List<System.Func<bool>>(4);
        var candidateTypes = new List<CroustyBonusHUD.SelectedBonus>(4);
        var pet = bus.GetComponent<BusPetController>();
        var gps = bus.GetComponent<BusGPS>();
        var rice = bus.GetComponent<BusRiceThrower>();
        var flight = bus.GetComponent<BusFlightController>();
        if (gps == null) gps = FindAnyObjectByType<BusGPS>();

        if ((progression == null || progression.IsUnlocked(PowerUpInventory.PowerUpType.Pet)) && pet != null && pet.isActiveAndEnabled && pet.CanReceiveCharges)
        {
            candidates.Add(() => pet.AddCharges(3, 3));
            candidateTypes.Add(CroustyBonusHUD.SelectedBonus.Pet);
        }
        if ((progression == null || progression.IsUnlocked(PowerUpInventory.PowerUpType.GPS)) && gps != null && gps.isActiveAndEnabled && gps.CanReceiveCharges)
        {
            candidates.Add(() => gps.AddCharges(1, 1));
            candidateTypes.Add(CroustyBonusHUD.SelectedBonus.GPS);
        }
        if ((progression == null || progression.IsUnlocked(PowerUpInventory.PowerUpType.Rice)) && rice != null && rice.isActiveAndEnabled && rice.CanReceiveCharges)
        {
            candidates.Add(() => rice.AddCharges(3, 3));
            candidateTypes.Add(CroustyBonusHUD.SelectedBonus.Rice);
        }
        if ((progression == null || progression.IsUnlocked(PowerUpInventory.PowerUpType.Flight)) && flight != null && flight.isActiveAndEnabled && flight.CanReceiveCharges)
        {
            candidates.Add(() => flight.AddCharges(1, 1));
            candidateTypes.Add(CroustyBonusHUD.SelectedBonus.Flight);
        }
        if (candidates.Count == 0) return;
        int awardedIndex = Random.Range(0, candidates.Count);
        if (!candidates[awardedIndex]()) return;

        foreach (CroustyBonusHUD hud in FindObjectsByType<CroustyBonusHUD>())
            hud.SelectBonus(candidateTypes[awardedIndex]);

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        SetAvailable(false);
        if (respawnDelay > 0f) yield return new WaitForSeconds(respawnDelay);
        SetAvailable(true);
    }

    private void SetAvailable(bool value)
    {
        available = value;
        trigger.enabled = value;
        if (visual != null) visual.SetActive(value);
        else foreach (var r in renderers) r.enabled = value;
        if (value) transform.localPosition = startLocalPosition;
    }
}
