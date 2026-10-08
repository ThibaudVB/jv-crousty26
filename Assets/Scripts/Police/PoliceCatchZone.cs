using UnityEngine;
using UnityEngine.Events;

// Retains the scene's existing game-over event; capture is now driven by speed pressure.
[RequireComponent(typeof(BoxCollider))]
public class PoliceCatchZone : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float timeBeforeCaught = 5f;
    [SerializeField] private UnityEvent onPlayerCaught = new UnityEvent();
    private float catchTimer;
    private bool caught;
    public float CatchProgress01 => caught ? 1f : Mathf.Clamp01(catchTimer / Mathf.Max(0.1f, timeBeforeCaught));
    public bool IsCaught => caught;
    public float CaptureDurationSeconds => Mathf.Max(0.1f, timeBeforeCaught);

    private void Awake()
    {
        GetComponent<BoxCollider>().enabled = false;
    }

    public void AdvanceCapture(bool belowSafeSpeed, float deltaTime)
    {
        if (caught || !isActiveAndEnabled) return;
        if (!belowSafeSpeed)
        {
            catchTimer = 0f;
            return;
        }
        catchTimer += Mathf.Max(0f, deltaTime);
        if (catchTimer + 0.0001f < Mathf.Max(0.1f, timeBeforeCaught)) return;
        caught = true;
        onPlayerCaught.Invoke();
    }

    private void OnDisable()
    {
        if (!caught) catchTimer = 0f;
    }
}
