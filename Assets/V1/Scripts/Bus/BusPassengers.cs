using System;
using UnityEngine;

namespace V1
{
    // Compte les passagers montés dans le bus.
    public class BusPassengers : MonoBehaviour
    {
        public int passengerCount;

        // Prévient le HUD à chaque nouveau passager
        public event Action<int> Changed;

        public void AddPassenger()
        {
            passengerCount++;
            Changed?.Invoke(passengerCount);
        }
    }
}
