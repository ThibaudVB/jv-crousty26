using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace V1
{
    // Klaxon du bus : tant que la touche est appuyée, le klaxon sonne.
    // Les voitures du trafic devant le bus se rangent sur le côté,
    // et les passagers qui attendent à un arrêt se dépêchent.
    // Ajouté automatiquement par BusController.
    public class BusHorn : MonoBehaviour
    {
        [Header("Touches")]
        public Key hornKey = Key.H;
        [Tooltip("Bouton du haut de la manette (Y / Triangle)")]
        public bool useGamepad = true;

        [Header("Son")]
        [Range(0f, 1f)] public float volume = 0.8f;

        [Header("Effet sur le trafic")]
        [Tooltip("Distance à laquelle les voitures entendent le klaxon")]
        public float hearingDistance = 40f;
        [Tooltip("Angle du cône devant le bus (degrés). Les voitures hors du cône ne réagissent pas.")]
        public float hearingAngle = 70f;
        [Tooltip("Délai entre deux alertes envoyées au trafic pendant qu'on klaxonne")]
        public float alertInterval = 0.3f;

        // Déclenché au début de chaque coup de klaxon
        public event Action Honked;

        public bool IsHonking { get; private set; }

        private AudioSource source;
        private float alertTimer;

        private void Awake()
        {
            source = ProceduralAudio.CreateSource(gameObject, ProceduralAudio.Horn, true, 0f, 10f, 150f);
        }

        private void Update()
        {
            bool pressed = HornPressed();

            if (pressed && !IsHonking)
            {
                source.Play();
                alertTimer = 0f;
                Honked?.Invoke();
            }
            IsHonking = pressed;

            // Petit fondu pour éviter les claquements au début et à la fin
            source.volume = Mathf.MoveTowards(source.volume, IsHonking ? volume : 0f, Time.deltaTime * 12f);
            if (!IsHonking && source.volume <= 0f && source.isPlaying)
                source.Stop();

            if (IsHonking)
            {
                alertTimer -= Time.deltaTime;
                if (alertTimer <= 0f)
                {
                    alertTimer = alertInterval;
                    AlertAround();
                }
            }
        }

        private bool HornPressed()
        {
            if (Keyboard.current != null && Keyboard.current[hornKey].isPressed)
                return true;

            return useGamepad && Gamepad.current != null && Gamepad.current.buttonNorth.isPressed;
        }

        private void AlertAround()
        {
            Vector3 position = transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float minDot = Mathf.Cos(hearingAngle * Mathf.Deg2Rad);

            // Copie de la liste : une voiture peut être détruite pendant la boucle
            foreach (TrafficCarNavMesh car in TrafficCarNavMesh.Active.ToArray())
            {
                if (car == null)
                    continue;

                Vector3 toCar = car.transform.position - position;
                toCar.y = 0f;
                float distance = toCar.magnitude;

                if (distance > hearingDistance || distance < 0.1f)
                    continue;

                if (Vector3.Dot(forward, toCar / distance) < minDot)
                    continue;

                car.HearHorn(transform);
            }

            foreach (BusStopPassenger passenger in BusStopPassenger.Active.ToArray())
            {
                if (passenger != null &&
                    (passenger.transform.position - position).sqrMagnitude <= hearingDistance * hearingDistance)
                    passenger.HearHorn(transform);
            }
        }
    }
}
