using UnityEngine;
using UnityEngine.Serialization;

namespace Jojo
{
    // Base commune du magasin et des adresses : une zone ronde (mesurée à plat)
    // signalée par un faisceau lumineux.
    public abstract class BeaconZone : MonoBehaviour
    {
        [FormerlySerializedAs("pickupRadius")]
        [FormerlySerializedAs("deliveryRadius")]
        [SerializeField] protected float radius = 8f;

        [Header("Marqueur")]
        [Tooltip("Optionnel : objet affiché comme marqueur. Si vide, un faisceau lumineux est créé.")]
        [SerializeField] protected GameObject marker;
        [SerializeField] protected Color markerColor = Color.white;
        [SerializeField] protected float beamHeight = 150f;
        [SerializeField] protected float beamWidth = 3f;

        public Color MarkerColor => markerColor;

        protected virtual void Awake()
        {
            if (marker == null)
                marker = JojoVisuals.CreateBeacon(transform, markerColor, beamHeight, beamWidth);
            else if (!marker.scene.IsValid())
                // Prefab glissé depuis le dossier Project : on en place une copie dans la scène
                marker = Instantiate(marker, transform);
        }

        public bool IsInRange(Vector3 position)
        {
            return DistanceTo(position) <= radius;
        }

        // Distance en ignorant la hauteur
        public float DistanceTo(Vector3 position)
        {
            Vector3 here = transform.position;
            position.y = 0f;
            here.y = 0f;
            return Vector3.Distance(position, here);
        }

        // Cercle à plat : la zone réelle est un cylindre, pas une sphère
        private void OnDrawGizmos()
        {
            const int segments = 48;
            Gizmos.color = markerColor;

            Vector3 center = transform.position;
            Vector3 previous = center + Vector3.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
