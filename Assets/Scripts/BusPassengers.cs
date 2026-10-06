using UnityEngine;


public class BusPassengers : MonoBehaviour
{
    public int passengerCount;

    public void AddPassenger()
    {
        passengerCount++;
        Debug.Log($"Passagers dans le bus : {passengerCount}");
    }
}