using UnityEngine;

namespace V1
{
    // Répliques du gérant selon ce qui se passe : police, dérapage, état de la tournée.
    public static class GerantLines
    {
        // Durée pendant laquelle le gérant commente un boost (s)
        private const float BoostCommentDuration = 2.5f;

        // bus et police peuvent être null (scène sans police, par exemple)
        public static string For(DeliveryManager manager, BusController bus, PoliceChaseManager police)
        {
            // La police passe avant tout le reste
            if (police != null && police.ChaseStarted && !police.IsEscaping)
                return police.CatchProgress01 > 0.6f
                    ? "Ils vont nous coincer ! ACCÉLÈRE !"
                    : $"Les flics ! Repasse au-dessus de {police.SafeSpeedKmh:0} km/h !";

            if (bus != null && Time.time - bus.LastBoostTime < BoostCommentDuration)
                return bus.LastBoostLevel >= 3
                    ? "WOW ! Ça c'est du dérapage de pro !"
                    : "Joli dérapage ! Continue comme ça !";

            if (!manager.HasStarted)
                return "Hop hop hop ! Les commandes attendent, on se bouge !";

            if (manager.AllDelivered)
                return "Bien joué ! Reviens au magasin, y'a d'autres commandes !";

            if (manager.PointInRange != null && !manager.AutoDeliver)
                return $"Appuie sur {manager.DeliverKey.ToString().ToUpper()} pour lancer le crousty !";

            if (manager.CroustyLeft > 0)
                return manager.CroustyLeft == 1
                    ? "Plus qu'une commande, on lâche rien !"
                    : $"Encore {manager.CroustyLeft} commandes, les clients ont faim !";

            return "";
        }
    }
}
