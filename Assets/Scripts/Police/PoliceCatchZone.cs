using UnityEngine;
using UnityEngine.Events;

// Arrest meter of the bus. PoliceChaseManager fills it while officers pin the bus
// down; when it is full the defeat event fires once.
[RequireComponent(typeof(BoxCollider))]
public class PoliceCatchZone : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float timeBeforeCaught = 5f;
    [SerializeField] private UnityEvent onPlayerCaught = new UnityEvent();
    public BoxCollider CaptureCollider => GetComponent<BoxCollider>();
    private float progress;
    private bool caught;
    public float CatchProgress01 => caught ? 1f : progress;
    public bool IsCaught => caught;
    public float CaptureDurationSeconds => Mathf.Max(0.1f, timeBeforeCaught);

    private void Awake()
    {
        CaptureCollider.isTrigger = true;
        CaptureCollider.enabled = true;
    }

    public void SetProgress(float value)
    {
        if (caught || !isActiveAndEnabled) return;
        progress = Mathf.Clamp01(value);
        if (progress < 1f) return;
        caught = true;
        onPlayerCaught.Invoke();
    }

    // Former API: a timer that only completes while touching the capture zone.
    public void AdvanceCapture(bool belowSafeSpeed, float deltaTime, bool touchingCaptureZone = false)
    {
        if (!belowSafeSpeed) { SetProgress(0f); return; }
        float next = progress + Mathf.Max(0f, deltaTime) / CaptureDurationSeconds;
        SetProgress(touchingCaptureZone ? next : Mathf.Min(next, .999f));
    }

    private void OnDisable()
    {
        if (!caught) progress = 0f;
    }
}
