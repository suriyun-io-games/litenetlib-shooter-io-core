using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PickupSpawnData
{
    public PickupEntity pickupPrefab;
    [Range(1, 100)]
    public int amount;
}
