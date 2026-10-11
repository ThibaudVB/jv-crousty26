using System;
using UnityEngine;
using System.Collections;

public class BusStopPassenger : MonoBehaviour
{
    public float walkSpeed = 2f;
    public float startRunning = 5f;
    public float turnSpeed = 10f;
    public float enterDistance = 0.6f;

    private Transform target;
    private Action onBoarded;
    private bool walking;

    public void BoardBus(Transform door, Action onBoardedCallback)
    {
        target = door;
        onBoarded = onBoardedCallback;
        Debug.Log($"Le passager {name} commence à marcher vers la porte du bus {door.name}");

        // Ca évite qu'il pousse le bus en marchant ;)
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        walking = true;
        StartCoroutine(StartRunning());

    }

    private void Update()
    {
        if (!walking) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= enterDistance)
        {
            walking = false;
            onBoarded?.Invoke();
            gameObject.SetActive(false);
            return;
        }

        Vector3 dir = toTarget.normalized;
        transform.position += dir * walkSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            turnSpeed * Time.deltaTime
        );
    }

    private IEnumerator StartRunning()
    {
        yield return new WaitForSeconds(5f);
        Debug.Log($"Le passager {name} commence à courir vers la porte du bus {target.name}");
        walkSpeed = startRunning ;
    }
}