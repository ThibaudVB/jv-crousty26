using System.Collections.Generic;
using UnityEngine;

// Une flaque de riz posée sur la route.
// Elle est créée par BusRiceThrower quand le bus jette du riz, puis disparaît toute seule.
// Les grains de riz visibles sont ses enfants : ils disparaissent avec elle.
//
// Rien à placer à la main : ce script est ajouté par le code à chaque jet.
// Pour savoir si une position est sur du riz : RiceZone.IsOnRice(position)
public class RiceZone : MonoBehaviour
{
    // Toutes les flaques présentes dans la scène
    public static readonly List<RiceZone> All = new List<RiceZone>();

    [Tooltip("Rayon de la flaque (m)")]
    public float radius = 6f;
    [Tooltip("Durée avant que la flaque disparaisse (s)")]
    public float duration = 10f;

    private float timeLeft;

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Start()
    {
        timeLeft = duration;
    }

    private void Update()
    {
        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Pendant la dernière seconde, la flaque rétrécit jusqu'à disparaître
        if (timeLeft < 1f)
            transform.localScale = Vector3.one * timeLeft;
    }

    // Vrai si la position est dans cette flaque (sans compter la hauteur)
    public bool Contains(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        if (Mathf.Abs(offset.y) > 2.5f) return false;
        offset.y = 0f;
        return offset.magnitude <= radius * transform.localScale.x;
    }

    // Vrai si la position est dans au moins une flaque
    public static bool IsOnRice(Vector3 position)
    {
        foreach (RiceZone zone in All)
        {
            if (zone.Contains(position))
                return true;
        }
        return false;
    }

    // Dessine la flaque dans la vue Scene
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
