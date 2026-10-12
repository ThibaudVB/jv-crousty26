using UnityEngine;

// À mettre sur le bus : au contact d'un objet dont le nom commence par "Visuel Garage",
// les flammes s'éteignent. Marche avec la génération aléatoire (pas besoin de tag).
public class BusGarageRepair : MonoBehaviour
{
    [Tooltip("Début du nom des objets garage (Visuel Garage_01, Visuel Garage_02, ...)")]
    public string garageNamePrefix = "Visuel Garage";

    [Tooltip("Affiche dans la Console le nom de chaque objet touché (pour vérifier les noms)")]
    public bool logTouched = false;

    private BusDamage damage;

    private void OnCollisionEnter(Collision collision)
    {
        Check(collision.collider.transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        Check(other.transform);
    }

    // Le nom peut être sur l'objet touché ou sur un de ses parents
    private void Check(Transform other)
    {
        if (logTouched) Debug.Log($"{name} touche : {other.name}", other);

        for (Transform t = other; t != null; t = t.parent)
        {
            if (t.name.StartsWith(garageNamePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                Repair();
                return;
            }
        }
    }

    private void Repair()
    {
        // BusDamage est ajouté au runtime par le DeliveryManager : on le cherche à la demande
        if (damage == null) damage = GetComponent<BusDamage>();
        if (damage != null) damage.ExtinguishFire();
    }
}