using UnityEngine;
using UnityEngine.Events;

// Le delai commence au spawn; la defaite exige aussi le contact avec cette zone.
[RequireComponent(typeof(BoxCollider))]
public class PoliceCatchZone : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float timeBeforeCaught = 5f;
    [SerializeField] private UnityEvent onPlayerCaught = new UnityEvent();
    public BoxCollider CaptureCollider => GetComponent<BoxCollider>();
    private float catchTimer;
    private bool caught;
    public float CatchProgress01 => caught ? 1f : Mathf.Clamp01(catchTimer / Mathf.Max(0.1f, timeBeforeCaught));
    public bool IsCaught => caught;
    public float CaptureDurationSeconds => Mathf.Max(0.1f, timeBeforeCaught);

    private void Awake()
    {
        CaptureCollider.isTrigger = true;
        CaptureCollider.enabled = true;
    }

    public void AdvanceCapture(bool belowSafeSpeed, float deltaTime, bool touchingCaptureZone = false)
    {
        if (caught || !isActiveAndEnabled) return;
        if (!belowSafeSpeed)
        {
            catchTimer = 0f;
            return;
        }
        catchTimer += Mathf.Max(0f, deltaTime);
        if (catchTimer + 0.0001f < Mathf.Max(0.1f, timeBeforeCaught)) return;
        if (!touchingCaptureZone) return;
        caught = true;
        onPlayerCaught.Invoke();
    }

    private void OnDisable()
    {
        if (!caught) catchTimer = 0f;
    }
}
