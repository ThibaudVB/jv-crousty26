#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class PoliceBalanceChecks : MonoBehaviour
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private int failures;
    private static object Get(object obj, string field)
    {
        var member = obj.GetType().GetField(field, Flags);
        return member != null ? member.GetValue(obj) : obj.GetType().GetProperty(field, Flags).GetValue(obj);
    }
    private static void Set(object obj, string field, object value) => (obj.GetType().GetField(field, Flags) ?? obj.GetType().GetField("<" + field + ">k__BackingField", Flags)).SetValue(obj, value);
    private static void Call(object obj, string method) => obj.GetType().GetMethod(method, Flags).Invoke(obj, null);
    private void Check(bool good, string label) { if (!good) failures++; Debug.Log((good ? "BALANCE_PASS " : "BALANCE_FAIL ") + label); }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        new GameObject("Police balance checks").AddComponent<PoliceBalanceChecks>();
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        var manager = DeliveryManager.Instance;
        var police = FindAnyObjectByType<PoliceChaseManager>();
        police.enabled = false;
        var air = police.GetComponent<PoliceAirSupport>(); air.enabled = false;
        police.BusBody.isKinematic = true;
        police.BusBody.GetComponent<BusController>().enabled = false;
        Set(manager, "TotalDelivered", 0); Set(manager, "runsLoaded", 1); Set(police, "heat", 1);
        Check(police.DifficultyLevel == 0 && (int)Get(police, "CurrentPatrolCount") == 1, "BASE starts with one patrol and minimum pressure");
        Check((int)Get(police, "PursuitLimit") == 1 && (int)Get(police, "RoadblockLimit") == 0, "tutorial starts with one pursuer and no roadblocks");
        Check(police.ChaseSpeed * 3.6f < 132 && (float)Get(police, "ArrestDuration") >= 4 && (float)Get(police, "SpottingDuration") >= 1, "easy starting speed, detection and arrest timing");
        Call(air, "Update");
        Check(!(bool)Get(air, "Eligible") && Get(air, "helicopter") == null, "no helicopter in first round even after time passes");
        Set(manager, "runsLoaded", 2); Call(air, "Update");
        Check((bool)Get(air, "Eligible") && (float)Get(air, "nextSpawn") >= Time.time + 17, "second round unlocks helicopter with an arrival delay");
        Check((float)Get(air, "DropInterval") == 6, "second round keeps introductory six-second drops");
        float previousInterval = 6;
        bool increasingDrops = true;
        for (int round = 3; round <= 12; round++)
        {
            Set(manager, "runsLoaded", round);
            float interval = (float)Get(air, "DropInterval");
            increasingDrops &= interval <= previousInterval && interval >= 1;
            if (round == 3) Check(Mathf.Abs(interval - 4.5f) < .001f, "drop pressure rises immediately in round three");
            previousInterval = interval;
        }
        Check(increasingDrops, "later rounds tighten drops without going below one second");
        Set(manager, "runsLoaded", 2);
        float previous = -1;
        bool gradual = true, bounded = true;
        foreach (int count in new[] {0, 3, 8, 18, 33, 48, 80})
        {
            Set(manager, "TotalDelivered", count); Set(police, "heat", (int)Get(police, "StartingHeat"));
            float pressure = police.ProgressionPressure;
            gradual &= pressure >= previous; previous = pressure;
            bounded &= (int)Get(police, "PursuitLimit") <= 6 && (int)Get(police, "CurrentPatrolCount") <= 4 && (int)Get(police, "RoadblockLimit") <= 3;
            Debug.Log("BALANCE_ROW delivered=" + count + " pressure=" + pressure + " patrols=" + Get(police, "CurrentPatrolCount") + " pursuers=" + Get(police, "PursuitLimit") + " kmh=" + police.ChaseSpeed * 3.6f + " blocks=" + Get(police, "RoadblockLimit"));
            if (count == 3) Check(pressure == 0 && (int)Get(police, "RoadblockLimit") == 0, "tutorial completion stays easy");
        }
        Check(gradual && bounded, "pressure increases gradually and all vehicle/block caps hold");
        Check(police.ProgressionPressure == 1 && police.ChaseSpeed * 3.6f < 181 && police.RearRamCooldown <= 1.8f && (float)Get(police, "SearchDuration") >= 20, "late pursuit stays intense with lower maximum speed");
        Check((float)Get(air, "VisitDuration") <= 32 && (float)Get(air, "VisitCooldown") >= 60, "helicopter visits are short and well spaced at maximum pressure");
        Set(manager, "TotalDelivered", 3);
        Check((float)Get(air, "VisitDuration") == 22 && (float)Get(air, "VisitCooldown") == 95, "early helicopter is especially intermittent");
        Call(air, "Spawn");
        Check(Get(air, "helicopter") != null && (float)Get(air, "leaveAt") > Time.time, "helicopter can spawn with a finite visit window");
        Set(air, "leaveAt", Time.time - 1); Call(air, "Update");
        Check(Get(air, "helicopter") == null && (float)Get(air, "nextSpawn") >= Time.time + 94, "helicopter leaves even during an ongoing chase and waits before returning");
        Debug.Log("BALANCE_DONE failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
#endif
