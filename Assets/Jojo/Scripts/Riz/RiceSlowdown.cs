using System.Collections.Generic;
using UnityEngine;

// Ralentit les voitures de police qui roulent sur du riz (les flaques RiceZone),
// sans modifier le code de la police.
//
// A placer à la main sur le GameObject qui a le PoliceChaseManager.
// (Les voitures de police sont créées par le code pendant la partie :
//  on ne peut pas mettre le script directement sur elles, alors celui-ci s'occupe de toutes.)
//
// Comment ça marche :
// à chaque pas de physique, une voiture de police avance un peu en partant de sa position actuelle.
// Ce script passe juste avant elle : si elle est sur le riz, il la ramène en arrière
// d'une partie du chemin qu'elle vient de faire. Avec 0.3, elle ne garde que 30 % de son
// déplacement : elle roule donc à 30 % de sa vitesse.
//
// Tous les réglages se font dans l'Inspector, pas besoin de toucher au code.
[DefaultExecutionOrder(-100)] // FixedUpdate de ce script avant celui de la police
public class RiceSlowdown : MonoBehaviour
{
    [Tooltip("Vitesse de la police sur le riz (0.3 = 30 % de sa vitesse normale)")]
    [Range(0.05f, 1f)] public float speedOnRice = 0.3f;

    [Tooltip("Temps pour retrouver sa vitesse normale après être sorti du riz (s)")]
    [Min(0.01f)] public float recoverySeconds = 1.5f;

    [Tooltip("Coché : affiche un message dans la Console quand une voiture touche le riz (pour tester)")]
    public bool debugLog;

    // Ce qu'on retient pour chaque voiture
    private class CarInfo
    {
        public Vector3 lastPosition;    // position au pas de physique précédent
        public float speedFactor = 1f;  // 1 = vitesse normale
    }
    private readonly Dictionary<PoliceCarAI, CarInfo> cars = new Dictionary<PoliceCarAI, CarInfo>();

    // Au-delà de cette distance en un seul pas, la voiture a été téléportée : on ne la freine pas
    private const float TeleportDistance = 5f;

    private void FixedUpdate()
    {
        foreach (PoliceCarAI car in FindObjectsByType<PoliceCarAI>())
        {
            Rigidbody body = car.GetComponent<Rigidbody>();
            if (body == null)
                continue;

            // Première fois qu'on voit cette voiture : on la retient
            if (!cars.TryGetValue(car, out CarInfo info))
            {
                info = new CarInfo();
                info.lastPosition = body.position;
                cars.Add(car, info);
                continue;
            }

            UpdateSpeedFactor(car, info);

            // Chemin parcouru depuis le pas précédent (à plat, la hauteur ne change pas)
            Vector3 position = body.position;
            Vector3 moved = position - info.lastPosition;
            moved.y = 0f;

            bool slowed = info.speedFactor < 1f;
            bool teleported = moved.magnitude > TeleportDistance;
            if (slowed && !teleported)
            {
                // On ne garde qu'une partie du déplacement
                Vector3 slowerPosition = info.lastPosition + moved * info.speedFactor;
                slowerPosition.y = position.y;
                body.position = slowerPosition;
                position = slowerPosition;
            }

            info.lastPosition = position;
        }
    }

    // Sur le riz : ralentie tout de suite. Hors du riz : elle reprend sa vitesse petit à petit.
    private void UpdateSpeedFactor(PoliceCarAI car, CarInfo info)
    {
        bool onRice = RiceZone.IsOnRice(car.transform.position);

        if (onRice)
        {
            if (debugLog && info.speedFactor == 1f)
                Debug.Log(car.name + " roule sur le riz !", car);

            info.speedFactor = speedOnRice;
        }
        else
        {
            float recoverySpeed = (1f - speedOnRice) / recoverySeconds;
            info.speedFactor = Mathf.MoveTowards(info.speedFactor, 1f, recoverySpeed * Time.fixedDeltaTime);
        }
    }
}
