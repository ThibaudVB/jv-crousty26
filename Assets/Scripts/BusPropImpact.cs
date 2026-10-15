using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class BusPropImpact : MonoBehaviour
{
    internal static readonly HashSet<BusPropImpact> ActiveBuses = new HashSet<BusPropImpact>();
    private Rigidbody body;
    private Collider[] busColliders;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => ActiveBuses.Clear();

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        busColliders = GetComponentsInChildren<Collider>();
    }
    private void OnEnable()
    {
        ActiveBuses.Add(this);
        IgnoreActiveProps();
    }
    // Les roues peuvent etre ajoutees pendant l'Awake du controleur du bus.
    private void Start()
    {
        busColliders = GetComponentsInChildren<Collider>();
        IgnoreActiveProps();
    }
    private void OnDisable() => ActiveBuses.Remove(this);

    internal void IgnoreActiveProps()
    {
        foreach (MapImpactProp prop in MapImpactProp.ActiveProps)
            if (prop != null) IgnorePropCollisions(prop);
    }

    public bool HitProp(MapImpactProp prop)
        => isActiveAndEnabled && prop != null && prop.TryHit(body.linearVelocity);

    internal void IgnorePropCollisions(MapImpactProp prop)
    {
        // Ignore trees and lamps before contact, including slow approaches.
        if (!prop.HasBeenHit && !prop.passThroughBus) return;
        if (busColliders == null) busColliders = GetComponentsInChildren<Collider>();
        Collider[] propColliders = prop.GetComponentsInChildren<Collider>();
        foreach (Collider busCollider in busColliders)
            foreach (Collider propCollider in propColliders)
                if (busCollider.enabled && propCollider.enabled &&
                    !busCollider.isTrigger && !propCollider.isTrigger)
                    Physics.IgnoreCollision(busCollider, propCollider, true);
        // Les triggers de detection restent actifs. Le bus traverse aussi les objets deja tombes.
    }
}
