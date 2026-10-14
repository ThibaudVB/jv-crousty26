using UnityEngine;

// À mettre sur le bus : au contact d'un objet dont le nom commence par "Visuel Garage",
// les flammes s'éteignent. Marche avec la génération aléatoire (pas besoin de tag).
public class BusGarageRepair : MonoBehaviour
{
    [Tooltip("Début du nom des objets garage (Visuel Garage_01, Visuel Garage_02, ...)")]
    public string garageNamePrefix = "Visuel Garage";

    [Tooltip("Affiche dans la Console le nom de chaque objet touché (pour vérifier les noms)")]
    public bool logTouched = false;

    [Header("Son de réparation")]
    [Tooltip("Son joué quand le bus endommagé est réparé")]
    public AudioClip repairSound;
    [Range(0f, 1f)]
    public float repairSoundVolume = 1f;

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

        if (IsGarage(other)) Repair();
    }

    public bool IsGarage(Transform other)
    {
        for (Transform t = other; t != null; t = t.parent)
            if (t.name.StartsWith(garageNamePrefix, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void Repair()
    {
        // BusDamage est placé sur le bus dans la scène
        if (damage == null) damage = GetComponent<BusDamage>();
        if (damage == null) return;

        bool wasDamaged = damage.IsDamaged;
        damage.ExtinguishFire();
        if (wasDamaged && repairSound != null)
            AudioSource.PlayClipAtPoint(repairSound, transform.position, repairSoundVolume);
    }
}
