using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BusStopZone : MonoBehaviour
{
    [Header("Passagers")]
    public List<BusStopPassenger> waitingPassengers = new List<BusStopPassenger>();
    public float delayBetweenPassengers = 0.5f;

    [Header("Arrêt du bus")]
    [Tooltip("Vitesse en dessous de laquelle le bus est considéré à l'arrêt")]
    public float stopSpeedKmh = 1f;
    [Tooltip("Temps (s) pendant lequel le bus doit rester à l'arrêt")]
    public float stopDuration = 0.5f;
    [Tooltip("Le centre du bus doit être dans la zone")]
    public bool requireBusCenterInside = true;

    [Header("Visuel (optionnel)")]
    public Renderer zoneRenderer;
    public Color waitingColor = new Color(1f, 0f, 0f, 0.5f);
    public Color readyColor = new Color(0f, 1f, 0f, 0.5f);

    private Collider zoneCollider;
    private BusController busInside;
    private Rigidbody busBody;
    private float stoppedTimer;
    private bool boarding;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
        SetColor(waitingColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        BusController bus = other.GetComponentInParent<BusController>();
        if (bus == null) return;

        busInside = bus;
        busBody = bus.GetComponent<Rigidbody>();
        stoppedTimer = 0f;
    }

    private void OnTriggerExit(Collider other)
    {
        BusController bus = other.GetComponentInParent<BusController>();
        if (bus == null || bus != busInside) return;

        busInside = null;
        busBody = null;
        stoppedTimer = 0f;
        if (!boarding) SetColor(waitingColor);
    }

    private void Update()
    {
        if (boarding || busInside == null) return;

        if (IsBusStoppedInZone())
        {
            stoppedTimer += Time.deltaTime;
            SetColor(readyColor);

            if (stoppedTimer >= stopDuration)
                StartCoroutine(BoardRoutine(busInside));
        }
        else
        {
            stoppedTimer = 0f;
            SetColor(waitingColor);
        }
    }

    private bool IsBusStoppedInZone()
    {
        Vector3 v = busBody.linearVelocity;
        v.y = 0f;
        if (v.magnitude * 3.6f > stopSpeedKmh) return false;

        if (requireBusCenterInside)
        {
            Bounds b = zoneCollider.bounds;
            Vector3 p = busInside.transform.position;
            p.y = b.center.y; // on ignore la hauteur
            if (!b.Contains(p)) return false;
        }

        return true;
    }

    private IEnumerator BoardRoutine(BusController bus)
    {
        boarding = true;

        Transform door = bus.transform.Find("BusDoor");
        if (door == null) door = bus.transform;

        BusPassengers counter = bus.GetComponent<BusPassengers>();
        if (counter == null) counter = bus.gameObject.AddComponent<BusPassengers>();

        foreach (BusStopPassenger p in waitingPassengers.ToArray())
        {
            if (p == null) continue;
            p.BoardBus(door, counter.AddPassenger);
            yield return new WaitForSeconds(delayBetweenPassengers);
        }

        waitingPassengers.Clear();
    }

    private void SetColor(Color c)
    {
        if (zoneRenderer != null)
            zoneRenderer.material.color = c;
    }
}