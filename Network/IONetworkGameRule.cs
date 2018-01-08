using UnityEngine;
using UnityEngine.Networking;

public class IONetworkGameRule : BaseNetworkGameRule
{
    public IONetworkGameRule(SimpleLanNetworkManager manager) : base(manager)
    {
    }

    protected override void AddBot()
    {
        var gameInstance = GameInstance.Singleton;
        var botList = gameInstance.bots;
        var bot = botList[Random.Range(0, botList.Length)];
        var botEntity = Instantiate(gameInstance.botPrefab);
        botEntity.playerName = bot.name;
        botEntity.selectHead = bot.GetSelectHead();
        botEntity.selectCharacter = bot.GetSelectCharacter();
        botEntity.selectWeapons = bot.GetSelectWeapons();
        NetworkServer.Spawn(botEntity.gameObject);
        GameplayManager.Singleton.characters.Add(botEntity);
    }

    protected override void EndMatch()
    {
    }
}
