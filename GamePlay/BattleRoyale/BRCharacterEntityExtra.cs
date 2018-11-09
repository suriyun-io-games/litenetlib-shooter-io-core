using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(CharacterEntity))]
public class BRCharacterEntityExtra : NetworkBehaviour
{
    [SyncVar]
    public bool isSpawned;
    public bool isGroundOnce { get; private set; }

    private Transform tempTransform;
    public Transform TempTransform
    {
        get
        {
            if (tempTransform == null)
                tempTransform = GetComponent<Transform>();
            return tempTransform;
        }
    }

    private CharacterEntity tempCharacterEntity;
    public CharacterEntity TempCharacterEntity
    {
        get
        {
            if (tempCharacterEntity == null)
                tempCharacterEntity = GetComponent<CharacterEntity>();
            return tempCharacterEntity;
        }
    }
    private float botRandomSpawn;
    private bool botSpawnCalled;
    private bool botDeadRemoveCalled;
    private float lastCircleCheckTime;

    private void Awake()
    {
        TempCharacterEntity.enabled = false;
        TempCharacterEntity.IsHidding = true;
        var brGameManager = GameplayManager.Singleton as BRGameplayManager;
        var maxRandomDist = 30f;
        if (brGameManager != null)
        {
            if (NetworkServer.active && brGameManager.currentState != BRState.WaitingForPlayers)
            {
                NetworkServer.Destroy(TempCharacterEntity.gameObject);
                return;
            }
            maxRandomDist = brGameManager.spawnerMoveDuration * 0.25f;
        }
        botRandomSpawn = Random.Range(0f, maxRandomDist);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        var brGameManager = GameplayManager.Singleton as BRGameplayManager;
        if (brGameManager != null && brGameManager.currentState != BRState.WaitingForPlayers)
            GameNetworkManager.Singleton.StopHost();
    }

    private void Start()
    {
        TempCharacterEntity.onDead += OnDead;
    }

    private void OnDestroy()
    {
        TempCharacterEntity.onDead -= OnDead;
    }

    private void Update()
    {
        var brGameManager = GameplayManager.Singleton as BRGameplayManager;
        if (isServer)
        {
            if (brGameManager.currentState != BRState.WaitingForPlayers && Time.realtimeSinceStartup - lastCircleCheckTime >= 1f)
            {
                var currentPosition = TempTransform.position;
                currentPosition.y = 0;

                var centerPosition = brGameManager.currentCenterPosition;
                centerPosition.y = 0;
                var distance = Vector3.Distance(currentPosition, centerPosition);
                var currentRadius = brGameManager.currentRadius;
                if (distance > currentRadius)
                    TempCharacterEntity.Hp -= Mathf.CeilToInt(brGameManager.CurrentCircleHpRateDps * TempCharacterEntity.TotalHp);
                lastCircleCheckTime = Time.realtimeSinceStartup;
            }
        }
        if (brGameManager.currentState != BRState.WaitingForPlayers && !isSpawned)
        {
            if (isServer && !botSpawnCalled && TempCharacterEntity is BotEntity && brGameManager.CanSpawnCharacter(TempCharacterEntity))
            {
                botSpawnCalled = true;
                StartCoroutine(BotSpawnRoutine());
            }
            if (TempCharacterEntity.TempRigidbody.useGravity)
                TempCharacterEntity.TempRigidbody.useGravity = false;
            if (TempCharacterEntity.enabled)
                TempCharacterEntity.enabled = false;
            TempCharacterEntity.IsHidding = true;
            if (isServer || isLocalPlayer)
            {
                TempTransform.position = brGameManager.GetSpawnerPosition();
                TempTransform.rotation = brGameManager.GetSpawnerRotation();
            }
        }
        else if (brGameManager.currentState == BRState.WaitingForPlayers || isSpawned)
        {
            if (isServer && !botDeadRemoveCalled && TempCharacterEntity is BotEntity && TempCharacterEntity.IsDead)
            {
                botDeadRemoveCalled = true;
                StartCoroutine(BotDeadRemoveRoutine());
            }
            if (!TempCharacterEntity.TempRigidbody.useGravity)
                TempCharacterEntity.TempRigidbody.useGravity = true;
            if (!TempCharacterEntity.enabled)
                TempCharacterEntity.enabled = true;
            TempCharacterEntity.IsHidding = false;
        }
    }

    IEnumerator BotSpawnRoutine()
    {
        yield return new WaitForSeconds(botRandomSpawn);
        ServerCharacterSpawn();
    }

    IEnumerator BotDeadRemoveRoutine()
    {
        yield return new WaitForSeconds(5f);
        NetworkServer.Destroy(gameObject);
    }

    private void OnDead()
    {
        if (!isServer)
            return;
        var brGameplayManager = GameplayManager.Singleton as BRGameplayManager;
        if (brGameplayManager != null)
            RpcRankResult(BaseNetworkGameManager.Singleton.CountAliveCharacters() + 1);
    }

    IEnumerator ShowRankResultRoutine(int rank)
    {
        yield return new WaitForSeconds(3f);
        var ui = UIBRGameplay.Singleton;
        if (ui != null)
            ui.ShowRankResult(rank);
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (isSpawned && !isGroundOnce && collision.impulse.y > 0)
            isGroundOnce = true;
    }

    protected virtual void OnCollisionStay(Collision collision)
    {
        if (isSpawned && !isGroundOnce && collision.impulse.y > 0)
            isGroundOnce = true;
    }

    [Server]
    public void ServerCharacterSpawn()
    {
        var brGameplayManager = GameplayManager.Singleton as BRGameplayManager;
        if (!isSpawned && brGameplayManager != null)
        {
            isSpawned = true;
            RpcCharacterSpawned(brGameplayManager.SpawnCharacter(TempCharacterEntity) + new Vector3(Random.Range(-2.5f, 2.5f), 0, Random.Range(-2.5f, 2.5f)));
        }
    }

    [Command]
    public void CmdCharacterSpawn()
    {
        var brGameplayManager = GameplayManager.Singleton as BRGameplayManager;
        if (!isSpawned && brGameplayManager != null && brGameplayManager.CanSpawnCharacter(TempCharacterEntity))
            ServerCharacterSpawn();
    }

    [ClientRpc]
    public void RpcCharacterSpawned(Vector3 spawnPosition)
    {
        TempCharacterEntity.TempTransform.position = spawnPosition;
        TempCharacterEntity.TempRigidbody.useGravity = true;
        TempCharacterEntity.TempRigidbody.isKinematic = false;
    }

    [ClientRpc]
    public void RpcRankResult(int rank)
    {
        if (isLocalPlayer)
        {
            if (GameNetworkManager.Singleton.gameRule != null &&
                GameNetworkManager.Singleton.gameRule is BattleRoyaleNetworkGameRule)
                (GameNetworkManager.Singleton.gameRule as BattleRoyaleNetworkGameRule).SetRewards(rank);
            StartCoroutine(ShowRankResultRoutine(rank));
        }
    }
}
