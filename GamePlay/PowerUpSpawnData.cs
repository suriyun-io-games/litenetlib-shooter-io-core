using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PowerUpSpawnData {
    public PowerUpEntity powerUpPrefab;
    [Range(1, 100)]
    public int amount;
}
