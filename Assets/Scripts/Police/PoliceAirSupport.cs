using UnityEngine;

// Short, readable attack runs. Only an existing pursuit can call air support.
[DisallowMultipleComponent]
public sealed class PoliceAirSupport : MonoBehaviour
{
    private PoliceChaseManager police;
    private ProceduralCity city;
    private GameObject helicopterPrefab, bombPrefab, dumpsterPrefab, helicopter;
    private Transform rotor;
    private float nextAttack, leaveAt, nextDrop;
    private int drops;

    private void Start()
    {
        police = GetComponent<PoliceChaseManager>();
        city = FindAnyObjectByType<ProceduralCity>();
        helicopterPrefab = Resources.Load<GameObject>("Crousty/PoliceAir/Helicopter");
        bombPrefab = Resources.Load<GameObject>("Crousty/PoliceAir/Bomb");
        dumpsterPrefab = Resources.Load<GameObject>("Crousty/PoliceAir/Dumpster");
        nextAttack = Time.time + 30f;
        if (helicopterPrefab == null || bombPrefab == null || dumpsterPrefab == null)
        { Debug.LogError("Police air support: missing imported models.", this); enabled = false; }
    }

    private bool GameEnded => DeliveryManager.Instance != null && DeliveryManager.Instance.HasFailed;

    private void Update()
    {
        if (Time.timeScale <= 0f || police == null || police.BusBody == null) return;
        if (GameEnded) { ClearHelicopter(); return; }
        if (helicopter == null)
        {
            if (Time.time < nextAttack || !police.ChaseStarted || police.DifficultyLevel < 1 ||
                city == null || !city.IsAreaReady(police.BusPosition)) return;
            Spawn();
            return;
        }
        float dt = Time.deltaTime;
        if (rotor != null) rotor.Rotate(Vector3.up, 1800f * dt, Space.Self);
        Vector3 heading = police.BusVelocity.sqrMagnitude > 4f ? police.BusVelocity.normalized : police.BusBody.transform.forward;
        heading = Vector3.ProjectOnPlane(heading, Vector3.up).normalized;
        helicopter.transform.rotation = Quaternion.RotateTowards(helicopter.transform.rotation,
            Quaternion.LookRotation(heading), 55f * dt);
        // Stay in formation: no independent forward speed that lets the helicopter escape.
        Vector3 position = FormationPosition(helicopter.transform.forward);
        helicopter.transform.position = position;
        if (Time.time >= leaveAt || Vector3.Distance(position, police.BusPosition) > 260f)
        { ClearHelicopter(); return; }
        if (Time.time < nextDrop || drops >= 12 || !police.ChaseStarted) return;
        nextDrop = Time.time + Mathf.Lerp(1.15f, .75f, police.DifficultyLevel / 4f);
        Vector3 drop = position - Vector3.up * 2f;
        if (!city.IsAreaReady(drop)) return;
        // Drop on the projected driving line. The projectile stops tracking after release.
        drop += helicopter.transform.right * Random.Range(-.6f, .6f);
        bool dumpster = drops % 3 == 2;
        var hazard = new GameObject(dumpster ? "Poubelle incendiaire police" : "Bombe police");
        hazard.transform.position = drop;
        hazard.AddComponent<PoliceAirProjectile>().Initialize(dumpster ? dumpsterPrefab : bombPrefab,
            dumpster, police.BusBody);
        drops++;
    }

    private void Spawn()
    {
        Vector3 heading = Vector3.ProjectOnPlane(police.BusBody.transform.forward, Vector3.up).normalized;
        if (police.BusVelocity.sqrMagnitude > 4f) heading = police.BusVelocity.normalized;
        helicopter = new GameObject("Helicoptere de police - passage");
        PoliceModelVisual.Create(helicopterPrefab, helicopter.transform, new Vector3(14f, 0f, 0f), 90f);
        foreach (Transform part in helicopter.GetComponentsInChildren<Transform>())
            if (part.name == "Rotor_Main") { rotor = part; break; }
        helicopter.transform.SetPositionAndRotation(FormationPosition(heading),
            Quaternion.LookRotation(heading));
        drops = 0;
        nextDrop = Time.time + .6f;
        leaveAt = Time.time + 12f;
        nextAttack = Time.time + Random.Range(38f, 48f) - police.DifficultyLevel * 5f;
    }

    private Vector3 FormationPosition(Vector3 heading)
    {
        return police.BusPosition + heading * 55f + Vector3.up * 22f;
    }

    private void ClearHelicopter()
    {
        if (helicopter != null) Destroy(helicopter);
        helicopter = null; rotor = null;
    }
    private void OnDisable() => ClearHelicopter();
}
