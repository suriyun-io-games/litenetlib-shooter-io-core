using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;

[System.Serializable]
public struct BRCircle
{
    public SimpleSphereData circleData;
    public float shrinkDelay;
    public float shrinkDuration;
    [Range(0.01f, 1f)]
    public float hpRateDps;
}

[System.Serializable]
public struct BRPattern
{
    public BRCircle[] circles;
    public SimpleLineData spawnerMovement;
    public float spawnerMoveDuration;
}

public enum BRState : byte
{
    WaitingForPlayers,
    WaitingForFirstCircle,
    ShrinkDelaying,
    Shrinking,
    LastCircle,
}

public class BRGameplayManager : GameplayManager
{
    [Header("Battle Royale")]
    public float waitForPlayersDuration;
    public float waitForFirstCircleDuration;
    public SimpleCubeData spawnableArea;
    public BRPattern[] patterns;
    public GameObject circleObject;
    public GameObject airplanePrefab;

    private bool spawnedAirplane;
    private GameObject airplane;

    [Header("Sync Vars")]
    [SyncField]
    public int currentCircle;
    [SyncField]
    public float currentRadius;
    [SyncField]
    public Vector3 currentCenterPosition;
    [SyncField]
    public float nextRadius;
    [SyncField]
    public Vector3 nextCenterPosition;
    [SyncField]
    public BRState currentState;
    [SyncField]
    public float currentDuration;
    [SyncField(hook = "OnCurrentCountdownChanged")]
    public float currentCountdown;
    [SyncField]
    public Vector3 spawnerMoveFrom;
    [SyncField]
    public Vector3 spawnerMoveTo;
    [SyncField]
    public float spawnerMoveDuration;
    [SyncField(hook = "OnSpawnerMoveCountdownChanged")]
    public float spawnerMoveCountdown;
    [SyncField]
    public int countAliveCharacters;
    [SyncField]
    public int countAllCharacters;

    public float CurrentCircleHpRateDps { get; private set; }
    // make this as field to make client update smoothly
    public float CurrentCountdown { get; private set; }
    // make this as field to make client update smoothly
    public float SpawnerMoveCountdown { get; private set; }
    public readonly List<BRCharacterEntityExtra> SpawningCharacters = new List<BRCharacterEntityExtra>();
    private float currentShrinkDuration;
    private float startShrinkRadius;
    private Vector3 startShrinkCenterPosition;
    private BRPattern randomedPattern;
    private bool isInSpawnableArea;

    public override void OnStartServer()
    {
        currentCircle = 0;
        currentRadius = 0;
        currentState = BRState.WaitingForPlayers;
        currentDuration = currentCountdown = waitForPlayersDuration;
        CurrentCircleHpRateDps = 0;
        CurrentCountdown = 0;
        SpawnerMoveCountdown = 0;
        randomedPattern = patterns[Random.Range(0, patterns.Length)];
        isInSpawnableArea = false;
    }

    public override bool CanRespawn(CharacterEntity character)
    {
        return false;
    }

    public override bool CanReceiveDamage(CharacterEntity damageReceiver, CharacterEntity attacker)
    {
        if (base.CanReceiveDamage(damageReceiver, attacker))
            return damageReceiver.GetComponent<BRCharacterEntityExtra>().isSpawned;
        return false;
    }

    public override bool CanAttack(CharacterEntity character)
    {
        var networkGameplayManager = BaseNetworkGameManager.Singleton;
        if (networkGameplayManager != null && networkGameplayManager.IsMatchEnded)
            return false;
        var extra = character.GetComponent<BRCharacterEntityExtra>();
        return currentState == BRState.WaitingForPlayers || (extra.isSpawned && extra.isGroundOnce);
    }

    private void Update()
    {
        UpdateGameState();
        UpdateCircle();
        UpdateSpawner();
        if (circleObject != null)
        {
            circleObject.SetActive(currentState != BRState.WaitingForPlayers);
            circleObject.transform.localScale = Vector3.one * currentRadius * 2f;
            circleObject.transform.position = currentCenterPosition;
        }
        if (CurrentCountdown > 0)
            CurrentCountdown -= Time.deltaTime;
        if (SpawnerMoveCountdown > 0)
        {
            SpawnerMoveCountdown -= Time.unscaledDeltaTime;
            if (!spawnedAirplane)
            {
                spawnedAirplane = true;
                if (airplanePrefab != null)
                    airplane = Instantiate(airplanePrefab);
            }
            if (airplane != null)
            {
                airplane.transform.position = GetSpawnerPosition();
                airplane.transform.rotation = GetSpawnerRotation();
            }
        }
        else
        {
            if (spawnedAirplane)
            {
                if (airplane != null)
                    Destroy(airplane);
            }
        }
    }

    private void UpdateGameState()
    {
        if (!IsServer)
            return;

        currentCountdown -= Time.deltaTime;
        var networkGameManager = BaseNetworkGameManager.Singleton;
        var gameRule = networkGameManager.gameRule == null ? null : networkGameManager.gameRule as BattleRoyaleNetworkGameRule;
        var characters = networkGameManager.Characters;
        countAliveCharacters = networkGameManager.CountAliveCharacters();
        countAllCharacters = networkGameManager.maxConnections;
        BRCircle circle;
        switch (currentState)
        {
            case BRState.WaitingForPlayers:
                // Start game immediately when players are full
                if (currentCountdown <= 0)
                {
                    foreach (var character in characters)
                    {
                        if (character == null)
                            continue;
                        SpawningCharacters.Add(character.GetComponent<BRCharacterEntityExtra>());
                    }
                    spawnerMoveFrom = randomedPattern.spawnerMovement.GetFromPosition();
                    spawnerMoveTo = randomedPattern.spawnerMovement.GetToPosition();
                    spawnerMoveDuration = spawnerMoveCountdown = randomedPattern.spawnerMoveDuration;
                    if (gameRule != null)
                        gameRule.AddBots();
                    currentState = BRState.WaitingForFirstCircle;
                    currentDuration = currentCountdown = waitForFirstCircleDuration;
                    // Spawn powerup and pickup items
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
                break;
            case BRState.WaitingForFirstCircle:
                if (currentCountdown <= 0)
                {
                    currentCircle = 0;
                    if (TryGetCircle(out circle))
                    {
                        currentState = BRState.ShrinkDelaying;
                        currentDuration = currentCountdown = circle.shrinkDelay;
                        CurrentCircleHpRateDps = circle.hpRateDps;
                        startShrinkRadius = currentRadius = circle.circleData.radius;
                        startShrinkCenterPosition = currentCenterPosition = circle.circleData.transform.position;
                        nextRadius = circle.circleData.radius;
                        nextCenterPosition = circle.circleData.transform.position;
                    }
                    else
                    {
                        currentState = BRState.LastCircle;
                        currentDuration = currentCountdown = 0;
                    }
                }
                break;
            case BRState.ShrinkDelaying:
                if (currentCountdown <= 0)
                {
                    BRCircle nextCircle;
                    if (TryGetCircle(out circle) && TryGetCircle(currentCircle + 1, out nextCircle))
                    {
                        currentState = BRState.Shrinking;
                        currentShrinkDuration = currentDuration = currentCountdown = circle.shrinkDuration;
                        CurrentCircleHpRateDps = circle.hpRateDps;
                        startShrinkRadius = currentRadius = circle.circleData.radius;
                        startShrinkCenterPosition = currentCenterPosition = circle.circleData.transform.position;
                        nextRadius = nextCircle.circleData.radius;
                        nextCenterPosition = nextCircle.circleData.transform.position;
                    }
                    else
                    {
                        currentState = BRState.LastCircle;
                        currentDuration = currentCountdown = 0;
                    }
                }
                break;
            case BRState.Shrinking:
                if (currentCountdown <= 0)
                {
                    ++currentCircle;
                    BRCircle nextCircle;
                    if (TryGetCircle(out circle) && TryGetCircle(currentCircle + 1, out nextCircle))
                    {
                        currentState = BRState.ShrinkDelaying;
                        currentDuration = currentCountdown = circle.shrinkDelay;
                        CurrentCircleHpRateDps = circle.hpRateDps;
                    }
                    else
                    {
                        currentState = BRState.LastCircle;
                        currentDuration = currentCountdown = 0;
                    }
                }
                break;
            case BRState.LastCircle:
                currentDuration = currentCountdown = 0;
                break;
        }
    }

    private void UpdateCircle()
    {
        if (currentState == BRState.Shrinking)
        {
            var countdown = IsServer ? currentCountdown : CurrentCountdown;
            var interp = (currentShrinkDuration - countdown) / currentShrinkDuration;
            currentRadius = Mathf.Lerp(startShrinkRadius, nextRadius, interp);
            currentCenterPosition = Vector3.Lerp(startShrinkCenterPosition, nextCenterPosition, interp);
        }
    }

    private void UpdateSpawner()
    {
        if (!IsServer)
            return;

        if (currentState != BRState.WaitingForPlayers)
        {
            spawnerMoveCountdown -= Time.deltaTime;

            if (!isInSpawnableArea && IsSpawnerInsideSpawnableArea())
                isInSpawnableArea = true;

            if (isInSpawnableArea && !IsSpawnerInsideSpawnableArea())
            {
                var characters = SpawningCharacters;
                foreach (var character in characters)
                {
                    if (character == null)
                        continue;
                    character.ServerCharacterSpawn();
                }
                // Spawn players that does not spawned
                isInSpawnableArea = false;
            }
        }
    }

    public bool TryGetCircle(out BRCircle circle)
    {
        return TryGetCircle(currentCircle, out circle);
    }

    public bool TryGetCircle(int currentCircle, out BRCircle circle)
    {
        circle = new BRCircle();
        if (currentCircle < 0 || currentCircle >= randomedPattern.circles.Length)
            return false;
        circle = randomedPattern.circles[currentCircle];
        return true;
    }

    public Vector3 GetSpawnerPosition()
    {
        var countdown = IsServer ? spawnerMoveCountdown : SpawnerMoveCountdown;
        var interp = (spawnerMoveDuration - countdown) / spawnerMoveDuration;
        return Vector3.Lerp(spawnerMoveFrom, spawnerMoveTo, interp);
    }

    public Quaternion GetSpawnerRotation()
    {
        var heading = spawnerMoveTo - spawnerMoveFrom;
        heading.y = 0f;
        return Quaternion.LookRotation(heading, Vector3.up);
    }

    public bool CanSpawnCharacter(CharacterEntity character)
    {
        var extra = character.GetComponent<BRCharacterEntityExtra>();
        return IsServer && (extra == null || !extra.isSpawned) && IsSpawnerInsideSpawnableArea();
    }

    public bool IsSpawnerInsideSpawnableArea()
    {
        var position = GetSpawnerPosition();
        var dist = Vector3.Distance(position, spawnableArea.transform.position);
        return dist <= spawnableArea.size.x * 0.5f &&
            dist <= spawnableArea.size.z * 0.5f;
    }

    public Vector3 SpawnCharacter(CharacterEntity character)
    {
        return character.CacheTransform.position = GetSpawnerPosition();
    }

    protected void OnCurrentCountdownChanged(float currentCountdown)
    {
        CurrentCountdown = currentCountdown;
    }

    protected void OnSpawnerMoveCountdownChanged(float spawnerMoveCountdown)
    {
        SpawnerMoveCountdown = spawnerMoveCountdown;
    }
}
