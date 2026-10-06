using UnityEngine;

// Fait "respirer" la largeur d'un faisceau lumineux.
public class BeaconPulse : MonoBehaviour
{
    public float speed = 2.5f;
    public float amount = 0.25f;

    private Vector3 baseScale;

    private void Start()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * speed) * amount;
        transform.localScale = new Vector3(
            baseScale.x * pulse,
            baseScale.y,
            baseScale.z * pulse
        );
    }
}
