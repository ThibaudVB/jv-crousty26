namespace Jojo
{
    // Répliques du gérant selon l'état de la tournée.
    public static class GerantLines
    {
        public static string For(DeliveryManager manager)
        {
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
