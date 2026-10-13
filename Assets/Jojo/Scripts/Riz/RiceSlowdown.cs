using UnityEngine;

// Applies the rice penalty before the police driving step, without teleporting cars.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PoliceChaseManager))]
public class RiceSlowdown : MonoBehaviour
{
    [Range(0.05f, 1f)] public float speedOnRice = 0.3f;
    [Min(0.01f)] public float recoverySeconds = 1.5f;
    public bool debugLog;
    private PoliceChaseManager police;

    private void Awake() => police = GetComponent<PoliceChaseManager>();

    private void FixedUpdate()
    {
        if (police == null) return;
        foreach (var car in police.Cars)
        {
            if (car == null || car.IsDestroyed) continue;
            bool onRice = RiceZone.IsOnRice(car.transform.position);
            float factor = onRice ? speedOnRice : Mathf.MoveTowards(car.RiceSpeedFactor, 1f,
                (1f - speedOnRice) / Mathf.Max(.01f, recoverySeconds) * Time.fixedDeltaTime);
            if (debugLog && onRice && car.RiceSpeedFactor == 1f)
                Debug.Log(car.name + " roule sur le riz !", car);
            car.SetRiceSpeedFactor(factor);
        }
    }

    private void OnDisable()
    {
        if (police == null) return;
        foreach (var car in police.Cars)
            if (car != null && !car.IsDestroyed) car.SetRiceSpeedFactor(1f);
    }
}
