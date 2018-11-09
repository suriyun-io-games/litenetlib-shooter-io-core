using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class BattleRoyaleNetworkGameRule : IONetworkGameRule
{
    [Tooltip("Maximum amount of bots will be filled when start game")]
    public int fillBots = 10;
    public int endMatchCountDown = 10;
    [Tooltip("Rewards for each ranking, sort from high to low (1 - 10)")]
    public MatchReward[] rewards;
    public int EndMatchCountingDown { get; protected set; }
    public override bool HasOptionBotCount { get { return false; } }
    public override bool HasOptionMatchTime { get { return false; } }
    public override bool HasOptionMatchKill { get { return false; } }
    public override bool HasOptionMatchScore { get { return false; } }
    public override bool ShowZeroScoreWhenDead { get { return false; } }
    public override bool ShowZeroKillCountWhenDead { get { return false; } }
    public override bool ShowZeroAssistCountWhenDead { get { return false; } }
    public override bool ShowZeroDieCountWhenDead { get { return false; } }
    
    protected override void EndMatch()
    {
        networkManager.StartCoroutine(EndMatchRoutine());
    }

    public void SetRewards(int rank)
    {
        MatchRewardHandler.SetRewards(rank, rewards);
    }

    IEnumerator EndMatchRoutine()
    {
        EndMatchCountingDown = endMatchCountDown;
        while (EndMatchCountingDown > 0)
        {
            yield return new WaitForSeconds(1);
            --EndMatchCountingDown;
        }
        networkManager.StopHost();
    }

    public override bool RespawnCharacter(BaseNetworkGameCharacter character, params object[] extraParams)
    {
        var brGameplayManager = GameplayManager.Singleton as BRGameplayManager;
        // Can spawn player when the game is waiting for players state only
        if (brGameplayManager != null && brGameplayManager.currentState == BRState.WaitingForPlayers)
            return true;
        return false;
    }

    public override void OnUpdate()
    {
        var brGameplayManager = GameplayManager.Singleton as BRGameplayManager;
        if (brGameplayManager != null && brGameplayManager.currentState == BRState.WaitingForPlayers)
            return;
        var networkGameManager = BaseNetworkGameManager.Singleton;
        if (networkGameManager.CountAliveCharacters() <= 1 && !IsMatchEnded)
        {
            var hasUnspawnedCharacter = false;
            var characters = networkGameManager.Characters;
            foreach (var character in characters)
            {
                if (character == null)
                    continue;
                var extra = character.GetComponent<BRCharacterEntityExtra>();
                if (extra != null)
                {
                    if (!extra.isSpawned)
                    {
                        hasUnspawnedCharacter = true;
                        continue;
                    }
                    if (!character.IsDead)
                        extra.RpcRankResult(1);
                }
            }
            // If some characters are not spawned, won't end match
            if (!hasUnspawnedCharacter)
            {
                IsMatchEnded = true;
                EndMatch();
            }
        }
    }

    public override void AddBots()
    {
        if (fillBots <= 0)
            return;

        var botCount = networkManager.maxConnections - networkManager.Characters.Count;
        if (botCount > fillBots)
            botCount = fillBots;
        for (var i = 0; i < botCount; ++i)
        {
            var character = NewBot();
            if (character == null)
                continue;
            
            NetworkServer.Spawn(character.gameObject);
            networkManager.RegisterCharacter(character);
        }
    }
}
