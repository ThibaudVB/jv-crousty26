using UnityEngine;

namespace Jojo
{
    // Magasin Crousty : les commandes sont chargées automatiquement
    // quand le bus entre dans le rayon. Son faisceau reste toujours visible.
    public class CroustyShop : BeaconZone
    {
        [Tooltip("Texte du repère à l'écran, visible à travers les bâtiments")]
        [SerializeField] private string screenLabel = "Crousty";

        public string ScreenLabel => screenLabel;

        // Valeurs par défaut quand on ajoute le script dans l'éditeur
        private void Reset()
        {
            radius = 8f;
            markerColor = new Color(1f, 0.85f, 0.1f);
            beamWidth = 3f;
        }
    }
}
