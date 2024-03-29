﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;

public class GameplayManager : LiteNetLibBehaviour
{
    [System.Serializable]
    public struct RewardCurrency
    {
        public string currencyId;
        public IntAttribute amount;
    }
    public const float REAL_MOVE_SPEED_RATE = 0.1f;
    public static GameplayManager Singleton { get; private set; }
    [Header("Character stats")]
    public int maxLevel = 1000;
    public IntAttribute exp = new IntAttribute() { minValue = 20, maxValue = 1023050, growth = 2.5f };
    public IntAttribute rewardExp = new IntAttribute() { minValue = 8, maxValue = 409220, growth = 2.5f };
    public RewardCurrency[] rewardCurrencies;
    public IntAttribute killScore = new IntAttribute() { minValue = 10, maxValue = 511525, growth = 1f };
    public int baseMaxHp = 100;
    public int baseMaxArmor = 100;
    public int baseMoveSpeed = 30;
    public float baseWeaponDamageRate = 1f;
    public float maxWeaponDamageRate = 2f;
    public float baseReduceDamageRate = 0f;
    public float maxReduceDamageRate = 0.6f;
    public float baseBlockReduceDamageRate = 0.3f;
    public float maxBlockReduceDamageRate = 0.6f;
    public float baseArmorReduceDamage = 0.3f;
    public float maxArmorReduceDamage = 0.6f;
    public int addingStatPoint = 1;
    public float minAttackVaryRate = -0.07f;
    public float maxAttackVaryRate = 0.07f;
    public CharacterAttributes[] availableAttributes;
    [Header("Game rules")]
    public int watchAdsRespawnAvailable = 2;
    public float respawnDuration = 5f;
    public float invincibleDuration = 1.5f;
    public bool autoReload = true;
    public bool autoPickup = false;
    public bool respawnPickedupItems = true;
    public SpawnArea[] characterSpawnAreas;
    public SpawnArea[] powerUpSpawnAreas;
    public SpawnArea[] pickupSpawnAreas;
    public PowerUpSpawnData[] powerUps;
    public PickupSpawnData[] pickups;
    public readonly Dictionary<string, PowerUpEntity> PowerUpEntities = new Dictionary<string, PowerUpEntity>();
    public readonly Dictionary<string, PickupEntity> PickupEntities = new Dictionary<string, PickupEntity>();
    public readonly Dictionary<int, CharacterAttributes> Attributes = new Dictionary<int, CharacterAttributes>();
    private bool isRegisteredPrefabs;

    protected virtual void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;
    }

    public void RegisterPrefabs()
    {
        if (isRegisteredPrefabs)
            return;
        isRegisteredPrefabs = true;
        PowerUpEntities.Clear();
        foreach (var powerUp in powerUps)
        {
            var powerUpPrefab = powerUp.powerUpPrefab;
            if (powerUpPrefab != null)
                GameNetworkManager.Singleton.Assets.RegisterPrefab(powerUpPrefab.Identity);
            if (powerUpPrefab != null && !PowerUpEntities.ContainsKey(powerUpPrefab.name))
                PowerUpEntities.Add(powerUpPrefab.name, powerUpPrefab);
        }
        PickupEntities.Clear();
        foreach (var pickup in pickups)
        {
            var pickupPrefab = pickup.pickupPrefab;
            if (pickupPrefab != null)
                GameNetworkManager.Singleton.Assets.RegisterPrefab(pickupPrefab.Identity);
            if (pickupPrefab != null && !PickupEntities.ContainsKey(pickupPrefab.name))
                PickupEntities.Add(pickupPrefab.name, pickupPrefab);
        }
        Attributes.Clear();
        foreach (var availableAttribute in availableAttributes)
        {
            Attributes[availableAttribute.GetHashId()] = availableAttribute;
        }
    }

    public override void OnStartServer()
    {
        RegisterPrefabs();
        foreach (var powerUp in powerUps)
        {
            if (powerUp.powerUpPrefab == null)
                continue;
            for (var i = 0; i < powerUp.amount; ++i)
                SpawnPowerUp(powerUp.powerUpPrefab.name);
        }
        foreach (var pickup in pickups)
        {
            if (pickup.pickupPrefab == null)
                continue;
            for (var i = 0; i < pickup.amount; ++i)
                SpawnPickup(pickup.pickupPrefab.name);
        }
    }

    public override void OnStartClient()
    {

    }

    public void SpawnPowerUp(string prefabName)
    {
        SpawnPowerUp(prefabName, GetPowerUpSpawnPosition());
    }

    public void SpawnPowerUp(string prefabName, Vector3 position)
    {
        if (!IsServer || string.IsNullOrEmpty(prefabName))
            return;
        PowerUpEntity powerUpPrefab;
        if (PowerUpEntities.TryGetValue(prefabName, out powerUpPrefab)) {
            var powerUpEntity = Instantiate(powerUpPrefab, position, Quaternion.identity);
            powerUpEntity.prefabName = prefabName;
            Manager.Assets.NetworkSpawn(powerUpEntity.gameObject);
        }
    }

    public void SpawnPickup(string prefabName)
    {
        SpawnPickup(prefabName, GetPickupSpawnPosition());
    }

    public void SpawnPickup(string prefabName, Vector3 position)
    {
        if (!IsServer || string.IsNullOrEmpty(prefabName))
            return;
        PickupEntity pickupPrefab;
        if (PickupEntities.TryGetValue(prefabName, out pickupPrefab))
        {
            var pickupEntity = Instantiate(pickupPrefab, position, Quaternion.identity);
            pickupEntity.prefabName = prefabName;
            Manager.Assets.NetworkSpawn(pickupEntity.gameObject);
        }
    }
    
    public Vector3 GetCharacterSpawnPosition()
    {
        if (characterSpawnAreas == null || characterSpawnAreas.Length == 0)
            return Vector3.zero;
        return characterSpawnAreas[Random.Range(0, characterSpawnAreas.Length)].GetSpawnPosition();
    }

    public Vector3 GetPowerUpSpawnPosition()
    {
        if (powerUpSpawnAreas == null || powerUpSpawnAreas.Length == 0)
            return Vector3.zero;
        return powerUpSpawnAreas[Random.Range(0, powerUpSpawnAreas.Length)].GetSpawnPosition();
    }

    public Vector3 GetPickupSpawnPosition()
    {
        if (pickupSpawnAreas == null || pickupSpawnAreas.Length == 0)
            return Vector3.zero;
        return pickupSpawnAreas[Random.Range(0, pickupSpawnAreas.Length)].GetSpawnPosition();
    }

    public int GetExp(int currentLevel)
    {
        return exp.Calculate(currentLevel, maxLevel);
    }

    public int GetRewardExp(int currentLevel)
    {
        return rewardExp.Calculate(currentLevel, maxLevel);
    }

    public int GetKillScore(int currentLevel)
    {
        return killScore.Calculate(currentLevel, maxLevel);
    }

    public virtual bool CanRespawn(CharacterEntity character)
    {
        var networkGameplayManager = BaseNetworkGameManager.Singleton;
        if (networkGameplayManager != null)
        {
            if (networkGameplayManager.IsMatchEnded)
                return false;
        }
        return true;
    }

    public virtual bool CanReceiveDamage(CharacterEntity damageReceiver, CharacterEntity attacker)
    {
        var networkGameplayManager = BaseNetworkGameManager.Singleton;
        if (networkGameplayManager != null)
        {
            if (networkGameplayManager.IsMatchEnded)
                return false;
        }
        return true;
    }

    public virtual bool CanAttack(CharacterEntity character)
    {
        var networkGameplayManager = BaseNetworkGameManager.Singleton;
        if (networkGameplayManager != null)
        {
            if (networkGameplayManager.IsMatchEnded)
                return false;
        }
        return true;
    }
}
