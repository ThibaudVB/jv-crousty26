using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace V1
{
    /// <summary>
    /// Voiture IA : suit les TrafficRoad (sens unique) point par point,
    /// et choisit une sortie au hasard à chaque TrafficIntersection.
    /// Le NavMeshAgent sert à se déplacer et à rester sur la route entre deux points.
    /// Quand le bus klaxonne, la voiture se range sur la droite et ralentit un moment.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficCarNavMesh : MonoBehaviour
    {
        // Toutes les voitures actives (utilisé par le klaxon du bus)
        public static readonly List<TrafficCarNavMesh> Active = new List<TrafficCarNavMesh>();

        [Header("Parcours")]
        [Tooltip("Vide = la route la plus proche au démarrage")]
        public TrafficRoad startRoad;
        [Tooltip("Distance à un point à partir de laquelle on passe au suivant")]
        public float reachDistance = 3f;

        [Tooltip("Nom de l'Area NavMesh des routes (vide = toutes les zones)")]
        public string roadAreaName = "Road";

        [Header("Vitesse")]
        public float minSpeedKmh = 30f;
        public float maxSpeedKmh = 50f;
        public float acceleration = 8f;
        public float angularSpeed = 100f;

        [Header("Détection d'obstacles")]
        public float frontOffset = 2.5f;
        public float rayHeight = 0.6f;
        public float detectionDistance = 10f;
        public float stopDistance = 3f;
        public LayerMask obstacleLayers = ~0;
        public float brakeDeceleration = 20f;

        [Header("Anti-blocage")]
        public float stuckTime = 6f;

        [Header("Réaction au klaxon")]
        [Tooltip("Durée pendant laquelle la voiture reste rangée")]
        public float yieldDuration = 2.5f;
        [Tooltip("Décalage vers la droite (mètres)")]
        public float yieldOffset = 2.5f;
        [Range(0f, 1f)]
        [Tooltip("Vitesse gardée pendant qu'elle se range (part de la vitesse normale)")]
        public float yieldSpeedFactor = 0.3f;
        [Range(0f, 1f)]
        [Tooltip("Chance que la voiture klaxonne en retour")]
        public float honkBackChance = 0.3f;

        public bool IsYielding => yieldTimer > 0f;

        private NavMeshAgent agent;
        private TrafficRoad currentRoad;
        private int targetIndex;
        private float cruiseSpeed;
        private float stuckTimer;
        private bool braking;

        private float yieldTimer;
        private float yieldShifted;
        private AudioSource hornSource;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();

            // Le Rigidbody sert uniquement à ce que la voiture soit un obstacle
            // solide pour le bus : c'est le NavMeshAgent qui la déplace.
            GetComponent<Rigidbody>().isKinematic = true;

            cruiseSpeed = Random.Range(minSpeedKmh, maxSpeedKmh) / 3.6f;

            agent.speed = cruiseSpeed;
            agent.acceleration = acceleration;
            agent.angularSpeed = angularSpeed;
            agent.autoBraking = false;
            agent.stoppingDistance = 0f;

            if (!string.IsNullOrEmpty(roadAreaName))
            {
                int area = NavMesh.GetAreaFromName(roadAreaName);
                if (area >= 0)
                    agent.areaMask = 1 << area;
                else
                    Debug.LogWarning($"Area NavMesh '{roadAreaName}' introuvable : toutes les zones sont utilisées.", this);
            }
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void Start()
        {
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning("TrafficCarNavMesh : la voiture n'est pas sur le NavMesh.", this);
                enabled = false;
                return;
            }

            if (startRoad != null)
            {
                currentRoad = startRoad;
                targetIndex = 0;
            }
            else if (!FindNearestRoadPoint())
            {
                Debug.LogWarning("TrafficCarNavMesh : aucune TrafficRoad dans la scène.", this);
                enabled = false;
                return;
            }

            GoToTarget();
        }

        private void Update()
        {
            UpdateObstacleBraking();
            UpdateYield();

            // Point atteint (distance à plat) : on passe au suivant
            Vector3 toPoint = currentRoad.GetPoint(targetIndex) - transform.position;
            toPoint.y = 0f;

            if (toPoint.magnitude <= reachDistance)
            {
                AdvanceTarget();
                return;
            }

            // Anti-blocage : on relance le trajet vers le point actuel
            if (!braking && !IsYielding && agent.velocity.magnitude < 0.2f)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;

            if (stuckTimer >= stuckTime)
            {
                stuckTimer = 0f;
                GoToTarget();
            }
        }

        // ---------- KLAXON ----------

        // Appelé par le klaxon du bus
        public void HearHorn(Transform source)
        {
            if (!enabled)
                return;

            // Première fois qu'on l'entend : peut-être un petit coup de klaxon en retour
            if (!IsYielding && Random.value < honkBackChance)
                Invoke(nameof(HonkBack), Random.Range(0.3f, 0.8f));

            // Rester rangé tant que le bus klaxonne
            if (!IsYielding)
                yieldShifted = 0f;
            yieldTimer = yieldDuration;
        }

        private void UpdateYield()
        {
            if (yieldTimer <= 0f)
                return;

            yieldTimer -= Time.deltaTime;

            // Se décale progressivement sur la droite (sans sortir du NavMesh)
            if (yieldShifted < yieldOffset && agent.isOnNavMesh)
            {
                float step = Mathf.Min(yieldOffset - yieldShifted, 2.5f * Time.deltaTime);
                agent.Move(transform.right * step);
                yieldShifted += step;
            }
        }

        private void HonkBack()
        {
            if (hornSource == null)
                hornSource = ProceduralAudio.CreateSource(gameObject, ProceduralAudio.SmallHorn, false, 0.5f, 5f, 60f);

            hornSource.pitch = Random.Range(0.9f, 1.15f);
            hornSource.PlayOneShot(ProceduralAudio.SmallHorn, 0.5f);
            // Coupe le son au bout d'un court instant (le clip fait 1 s)
            Invoke(nameof(StopHonk), Random.Range(0.25f, 0.5f));
        }

        private void StopHonk()
        {
            if (hornSource != null)
                hornSource.Stop();
        }

        // ---------- PARCOURS ----------

        private void AdvanceTarget()
        {
            targetIndex++;

            // Fin de la route : intersection (sortie au hasard) ou route suivante
            if (targetIndex >= currentRoad.PointCount)
            {
                TrafficRoad next = currentRoad.GetNextRoad();

                if (next == null || next.PointCount == 0)
                {
                    Debug.LogWarning($"TrafficCarNavMesh : fin de route sans suite ({currentRoad.name}).", this);
                    agent.ResetPath();
                    enabled = false;
                    return;
                }

                currentRoad = next;
                targetIndex = 0;
            }

            GoToTarget();
        }

        private void GoToTarget()
        {
            agent.SetDestination(currentRoad.GetPoint(targetIndex));
        }

        private bool FindNearestRoadPoint()
        {
            TrafficRoad[] roads = FindObjectsByType<TrafficRoad>();

            float bestDist = float.MaxValue;
            TrafficRoad bestRoad = null;
            int bestIndex = 0;

            foreach (TrafficRoad road in roads)
            {
                for (int i = 0; i < road.PointCount; i++)
                {
                    float d = (road.GetPoint(i) - transform.position).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestRoad = road;
                        bestIndex = i;
                    }
                }
            }

            if (bestRoad == null)
                return false;

            currentRoad = bestRoad;

            // On vise le point le plus proche, ou le suivant si on est déjà dessus
            bool alreadyThere = bestDist <= reachDistance * reachDistance;
            targetIndex = alreadyThere ? Mathf.Min(bestIndex + 1, bestRoad.PointCount - 1) : bestIndex;
            return true;
        }

        private void UpdateObstacleBraking()
        {
            float limit = IsYielding ? cruiseSpeed * yieldSpeedFactor : cruiseSpeed;
            braking = false;

            Vector3 origin = transform.position
                + transform.forward * frontOffset
                + Vector3.up * rayHeight;

            if (Physics.Raycast(origin, transform.forward, out RaycastHit hit,
                    detectionDistance, obstacleLayers, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
            {
                braking = true;

                if (hit.distance <= stopDistance)
                {
                    limit = 0f;
                }
                else
                {
                    float t = (hit.distance - stopDistance) / (detectionDistance - stopDistance);
                    limit = Mathf.Min(limit, cruiseSpeed * Mathf.Clamp01(t));
                }
            }

            float rate = limit < agent.speed ? brakeDeceleration : acceleration;
            agent.speed = Mathf.MoveTowards(agent.speed, limit, rate * Time.deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 origin = transform.position
                + transform.forward * frontOffset
                + Vector3.up * rayHeight;
            Gizmos.DrawLine(origin, origin + transform.forward * detectionDistance);

            if (Application.isPlaying && currentRoad != null && targetIndex < currentRoad.PointCount)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, currentRoad.GetPoint(targetIndex));
            }
        }
    }
}
