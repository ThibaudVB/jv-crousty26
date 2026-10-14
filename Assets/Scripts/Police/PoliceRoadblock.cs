using UnityEngine;

// Marks every solid part of a roadblock, including its parked police cars.
// BusDamage applies contact damage; BusCarCrash must never clear these obstacles.
[DisallowMultipleComponent]
public sealed class PoliceRoadblock : MonoBehaviour
{
    public float CreatedAt { get; private set; }
    private void Awake() => CreatedAt = Time.time;
}
