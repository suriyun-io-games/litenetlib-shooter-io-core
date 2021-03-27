using UnityEngine;

public class BattleRoyaleNetworkGameRule : IONetworkGameRule
{
    [Tooltip("Maximum amount of bots will be filled when start game")]
    public int fillBots = 10;
    [Tooltip("Rewards for each ranking, sort from high to low (1 - 10)")]
    public MatchReward[] rewards;
    public override bool HasOptionBotCount { get { return false; } }
    public override bool HasOptionMatchTime { get { return false; } }
    public override bool HasOptionMatchKill { get { return false; } }
    public override bool HasOptionMatchScore { get { return false; } }
    public override bool ShowZeroScoreWhenDead { get { return false; } }
    public override bool ShowZeroKillCountWhenDead { get { return false; } }
    public override bool ShowZeroAssistCountWhenDead { get { return false; } }
    public override bool ShowZeroDieCountWhenDead { get { return false; } }

    public override void OnStartServer()
    {
        matchStartTime = Time.unscaledTime;
        teamScoreA = 0;
        teamScoreB = 0;
        teamKillA = 0;
        teamKillB = 0;
        IsMatchEnded = false;
    }

    public void SetRewards(int rank)
    {
        MatchRewardHandler.SetRewards(rank, rewards);
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
            }
        }
    }

    public override void AddBots()
    {
        if (fillBots <= 0)
            return;

        var botCount = NetworkManager.maxConnections - NetworkManager.Characters.Count;
        if (botCount > fillBots)
            botCount = fillBots;
        for (var i = 0; i < botCount; ++i)
        {
            var character = NewBot();
            if (character == null)
                continue;
            
            NetworkManager.Assets.NetworkSpawn(character.gameObject);
            NetworkManager.RegisterCharacter(character);
        }
    }
}
