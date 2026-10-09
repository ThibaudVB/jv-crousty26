using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace V1
{
    // Passager qui attend à un arrêt, puis marche (et finit par courir) jusqu'à la porte du bus.
    // Un coup de klaxon le fait se tourner vers le bus, ou courir s'il est déjà en route.
    public class BusStopPassenger : MonoBehaviour
    {
        // Passagers présents dans la scène (utilisé par le klaxon du bus)
        public static readonly List<BusStopPassenger> Active = new List<BusStopPassenger>();

        public float walkSpeed = 2f;
        public float startRunning = 5f;
        [Tooltip("Temps de marche avant de se mettre à courir (s)")]
        public float runAfterSeconds = 5f;
        public float turnSpeed = 10f;
        public float enterDistance = 0.6f;

        public bool IsBoarding => walking;

        private Transform target;
        private Action onBoarded;
        private bool walking;
        private Transform lookTarget;

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public void BoardBus(Transform door, Action onBoardedCallback)
        {
            target = door;
            onBoarded = onBoardedCallback;

            // Ça évite qu'il pousse le bus en marchant ;)
            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.enabled = false;

            walking = true;
            StartCoroutine(StartRunning());
        }

        // Appelé par le klaxon du bus
        public void HearHorn(Transform bus)
        {
            if (walking)
                walkSpeed = Mathf.Max(walkSpeed, startRunning);
            else
                lookTarget = bus;
        }

        private void Update()
        {
            if (!walking)
            {
                LookAtHorn();
                return;
            }

            // Le bus a disparu (scène rechargée, bus détruit...) : on arrête
            if (target == null)
            {
                walking = false;
                return;
            }

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

        // En attendant à l'arrêt, le passager regarde le bus qui a klaxonné
        private void LookAtHorn()
        {
            if (lookTarget == null)
                return;

            Vector3 toBus = lookTarget.position - transform.position;
            toBus.y = 0f;
            if (toBus.sqrMagnitude < 0.01f)
                return;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toBus),
                turnSpeed * 0.5f * Time.deltaTime
            );
        }

        private IEnumerator StartRunning()
        {
            yield return new WaitForSeconds(runAfterSeconds);
            walkSpeed = Mathf.Max(walkSpeed, startRunning);
        }
    }
}
