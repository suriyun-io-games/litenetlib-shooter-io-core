using UnityEngine;
using UnityEngine.Networking;

public class IONetworkGameRule : BaseNetworkGameRule
{
    public UIGameplay uiGameplayPrefab;
    public CharacterEntity overrideCharacterPrefab;
    public BotEntity overrideBotPrefab;
    public WeaponData[] startWeapons;

    public override bool HasOptionBotCount { get { return true; } }
    public override bool HasOptionMatchTime { get { return false; } }
    public override bool HasOptionMatchKill { get { return false; } }
    public override bool HasOptionMatchScore { get { return false; } }
    public override bool ShowZeroScoreWhenDead { get { return true; } }
    public override bool ShowZeroKillCountWhenDead { get { return true; } }
    public override bool ShowZeroAssistCountWhenDead { get { return true; } }
    public override bool ShowZeroDieCountWhenDead { get { return true; } }

    private string GetStartWeapons()
    {
        var selectWeapons = string.Empty;
        for (var i = 0; i < startWeapons.Length; ++i)
        {
            var startWeapon = startWeapons[i];
            if (!string.IsNullOrEmpty(selectWeapons))
                selectWeapons += "|";
            if (startWeapon != null)
                selectWeapons += startWeapon.GetId();
        }
        return selectWeapons;
    }

    protected override BaseNetworkGameCharacter NewBot()
    {
        var gameInstance = GameInstance.Singleton;
        var botList = gameInstance.bots;
        var bot = botList[Random.Range(0, botList.Length)];
        // Get character prefab
        BotEntity botPrefab = gameInstance.botPrefab;
        if (overrideBotPrefab != null)
            botPrefab = overrideBotPrefab;
        // Set character data
        var botEntity = Instantiate(botPrefab);
        botEntity.playerName = bot.name;
        botEntity.selectHead = bot.GetSelectHead();
        botEntity.selectCharacter = bot.GetSelectCharacter();
        botEntity.selectWeapons = bot.GetSelectWeapons();
        if (startWeapons != null && startWeapons.Length > 0)
            botEntity.selectWeapons = GetStartWeapons();
        return botEntity;
    }

    public virtual void NewPlayer(CharacterEntity character)
    {
        if (startWeapons != null && startWeapons.Length > 0)
            character.selectWeapons = GetStartWeapons();
    }

    protected override void EndMatch()
    {
    }

    public override bool CanCharacterRespawn(BaseNetworkGameCharacter character, params object[] extraParams)
    {
        var gameplayManager = GameplayManager.Singleton;
        var targetCharacter = character as CharacterEntity;
        return gameplayManager.CanRespawn(targetCharacter) && Time.unscaledTime - targetCharacter.deathTime >= gameplayManager.respawnDuration;
    }

    public override bool RespawnCharacter(BaseNetworkGameCharacter character, params object[] extraParams)
    {
        var isWatchedAds = false;
        if (extraParams.Length > 0 && extraParams[0] is bool)
            isWatchedAds = (bool)extraParams[0];

        var targetCharacter = character as CharacterEntity;
        var gameplayManager = GameplayManager.Singleton;
        // For IO Modes, character stats will be reset when dead
        if (!isWatchedAds || targetCharacter.watchAdsCount >= gameplayManager.watchAdsRespawnAvailable)
        {
            targetCharacter.ResetScore();
            targetCharacter.ResetKillCount();
            targetCharacter.ResetAssistCount();
            targetCharacter.Exp = 0;
            targetCharacter.level = 1;
            targetCharacter.statPoint = 0;
            targetCharacter.watchAdsCount = 0;
            targetCharacter.addStats = new CharacterStats();
            targetCharacter.Armor = 0;
        }
        else
            ++targetCharacter.watchAdsCount;

        return true;
    }

    public override void OnStartServer(BaseNetworkGameManager manager)
    {
        base.OnStartServer(manager);

        if (overrideCharacterPrefab != null && !ClientScene.prefabs.ContainsValue(overrideCharacterPrefab.gameObject))
            ClientScene.RegisterPrefab(overrideCharacterPrefab.gameObject);

        if (overrideBotPrefab != null && !ClientScene.prefabs.ContainsValue(overrideBotPrefab.gameObject))
            ClientScene.RegisterPrefab(overrideBotPrefab.gameObject);
    }

    public override void InitialClientObjects(NetworkClient client)
    {
        if (overrideCharacterPrefab != null && !ClientScene.prefabs.ContainsValue(overrideCharacterPrefab.gameObject))
            ClientScene.RegisterPrefab(overrideCharacterPrefab.gameObject);

        if (overrideBotPrefab != null && !ClientScene.prefabs.ContainsValue(overrideBotPrefab.gameObject))
            ClientScene.RegisterPrefab(overrideBotPrefab.gameObject);

        var ui = FindObjectOfType<UIGameplay>();
        if (ui == null && uiGameplayPrefab != null)
            ui = Instantiate(uiGameplayPrefab);
        if (ui != null)
            ui.gameObject.SetActive(true);
    }
}
