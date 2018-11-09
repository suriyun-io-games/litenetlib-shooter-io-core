using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(GameNetworkDiscovery))]
public class GameNetworkManager : BaseNetworkGameManager
{
    public static new GameNetworkManager Singleton
    {
        get { return singleton as GameNetworkManager; }
    }

    private JoinMessage MakeJoinMessage()
    {
        var msg = new JoinMessage();
        msg.playerName = PlayerSave.GetPlayerName();
        msg.selectHead = GameInstance.GetAvailableHead(PlayerSave.GetHead()).GetId();
        msg.selectCharacter = GameInstance.GetAvailableCharacter(PlayerSave.GetCharacter()).GetId();
        // Weapons
        var savedWeapons = PlayerSave.GetWeapons();
        var selectWeapons = "";
        foreach (var savedWeapon in savedWeapons)
        {
            if (!string.IsNullOrEmpty(selectWeapons))
                selectWeapons += "|";
            var data = GameInstance.GetAvailableWeapon(savedWeapon.Value);
            if (data != null)
                selectWeapons += data.GetId();
        }
        msg.selectWeapons = selectWeapons;
        return msg;
    }

    public override void OnClientConnect(NetworkConnection conn)
    {
        if (!clientLoadedScene)
        {
            // Ready/AddPlayer is usually triggered by a scene load completing. if no scene was loaded, then Ready/AddPlayer it here instead.
            ClientScene.Ready(conn);
            ClientScene.AddPlayer(conn, 0, MakeJoinMessage());
        }
    }

    public override void OnClientSceneChanged(NetworkConnection conn)
    {
        // always become ready.
        ClientScene.Ready(conn);

        bool addPlayer = (ClientScene.localPlayers.Count == 0);
        bool foundPlayer = false;
        for (int i = 0; i < ClientScene.localPlayers.Count; i++)
        {
            if (ClientScene.localPlayers[i].gameObject != null)
            {
                foundPlayer = true;
                break;
            }
        }
        // there are players, but their game objects have all been deleted
        if (!foundPlayer)
            addPlayer = true;

        if (addPlayer)
            ClientScene.AddPlayer(conn, 0, MakeJoinMessage());
    }

    public override void OnStartClient(NetworkClient client)
    {
        base.OnStartClient(client);
        client.RegisterHandler(new OpMsgCharacterAttack().OpId, ReadMsgCharacterAttack);
    }

    protected void ReadMsgCharacterAttack(NetworkMessage netMsg)
    {
        var msg = netMsg.ReadMessage<OpMsgCharacterAttack>();
        // Instantiates damage entities on clients only
        if (!NetworkServer.active)
            DamageEntity.InstantiateNewEntity(msg);
    }

    protected override BaseNetworkGameCharacter NewCharacter(NetworkReader extraMessageReader)
    {
        var joinMessage = extraMessageReader.ReadMessage<JoinMessage>();
        // Get character prefab
        CharacterEntity characterPrefab = GameInstance.Singleton.characterPrefab;
        if (gameRule != null && gameRule is IONetworkGameRule)
        {
            var ioGameRule = gameRule as IONetworkGameRule;
            if (ioGameRule.overrideCharacterPrefab != null)
                characterPrefab = ioGameRule.overrideCharacterPrefab;
        }
        var character = Instantiate(characterPrefab);
        // Set character data
        character.Hp = character.TotalHp;
        character.playerName = joinMessage.playerName;
        character.selectHead = joinMessage.selectHead;
        character.selectCharacter = joinMessage.selectCharacter;
        character.selectWeapons = joinMessage.selectWeapons;
        character.extra = joinMessage.extra;
        if (gameRule != null && gameRule is IONetworkGameRule)
        {
            var ioGameRule = gameRule as IONetworkGameRule;
            ioGameRule.NewPlayer(character);
        }
        return character;
    }

    protected override void UpdateScores(NetworkGameScore[] scores)
    {
        var rank = 0;
        foreach (var score in scores)
        {
            ++rank;
            if (BaseNetworkGameCharacter.Local != null && score.netId.Equals(BaseNetworkGameCharacter.Local.netId))
            {
                (BaseNetworkGameCharacter.Local as CharacterEntity).rank = rank;
                break;
            }
        }
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.UpdateRankings(scores);
    }

    protected override void KillNotify(string killerName, string victimName, string weaponId)
    {
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.KillNotify(killerName, victimName, weaponId);
    }

    [System.Serializable]
    public class JoinMessage : MessageBase
    {
        public string playerName;
        public string selectHead;
        public string selectCharacter;
        public string selectWeapons;
        public string extra;
    }
}
