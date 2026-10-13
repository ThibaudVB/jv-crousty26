using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class PowerUpBox : MonoBehaviour
{
    [Header("Contenu")]
    [Min(1)] public int chargesGiven = 1;

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

        var candidates = new List<System.Func<bool>>(4);
        var pet = other.GetComponentInParent<BusPetController>();
        var gps = other.GetComponentInParent<BusGPS>();
        var rice = other.GetComponentInParent<BusRiceThrower>();
        var flight = other.GetComponentInParent<BusFlightController>();
        if (gps == null) gps = FindAnyObjectByType<BusGPS>();

        if (pet != null && pet.CanReceiveCharges)
            candidates.Add(() => pet.AddCharges(chargesGiven, 3));
        if (gps != null && gps.CanReceiveCharges)
            candidates.Add(() => gps.AddCharges(chargesGiven, 3));
        if (rice != null && rice.CanReceiveCharges)
            candidates.Add(() => rice.AddCharges(chargesGiven, 3));
        if (flight != null && flight.CanReceiveCharges)
            candidates.Add(() => flight.AddCharges(chargesGiven, 3));
        if (candidates.Count == 0 || !candidates[Random.Range(0, candidates.Count)]()) return;

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