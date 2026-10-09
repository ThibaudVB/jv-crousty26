using UnityEngine;

namespace V1
{
    // Gyrophares rouge et bleu qui clignotent.
    // Ajouté automatiquement sur la voiture de police par PoliceCarAI.
    public class PoliceSiren : MonoBehaviour
    {
        public Color leftColor = new Color(1f, 0.1f, 0.1f);
        public Color rightColor = new Color(0.15f, 0.35f, 1f);
        [Tooltip("Changements de couleur par seconde")]
        public float flashRate = 4f;
        public float lightIntensity = 12f;
        public float lightRange = 18f;

        private Light leftLight;
        private Light rightLight;

        private void Start()
        {
            Bounds bounds = WorldBounds();
            Vector3 top = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y + 0.3f, bounds.center.z));
            float side = Mathf.Max(0.4f, bounds.extents.x * 0.4f);

            leftLight = CreateLight("Gyrophare rouge", top + Vector3.left * side, leftColor);
            rightLight = CreateLight("Gyrophare bleu", top + Vector3.right * side, rightColor);
        }

        private void Update()
        {
            if (leftLight == null)
                return;

            // Une couleur après l'autre
            bool leftOn = Mathf.Repeat(Time.time * flashRate, 2f) < 1f;
            leftLight.enabled = leftOn;
            rightLight.enabled = !leftOn;
        }

        private Light CreateLight(string name, Vector3 localPosition, Color color)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = localPosition;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = lightIntensity;
            light.range = lightRange;
            light.shadows = LightShadows.None;
            return light;
        }

        private Bounds WorldBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(transform.position + Vector3.up, new Vector3(2f, 1.5f, 4f));

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
