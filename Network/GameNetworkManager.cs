using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(GameNetworkDiscovery))]
public class GameNetworkManager : SimpleLanNetworkManager
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
        var savedWeapons = PlayerSave.GetWeapons();
        var selectWeapons = "";
        foreach (var savedWeapon in savedWeapons)
        {
            if (!string.IsNullOrEmpty(selectWeapons))
                selectWeapons += "|";
            selectWeapons += GameInstance.GetAvailableWeapon(savedWeapon.Value).GetId();
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
            if (autoCreatePlayer)
            {
                ClientScene.AddPlayer(conn, 0, MakeJoinMessage());
            }
        }
    }

    public override void OnClientSceneChanged(NetworkConnection conn)
    {
        // always become ready.
        ClientScene.Ready(conn);

        if (!autoCreatePlayer)
        {
            return;
        }

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
        if (!foundPlayer)
        {
            // there are players, but their game objects have all been deleted
            addPlayer = true;
        }
        if (addPlayer)
        {
            ClientScene.AddPlayer(conn, 0, MakeJoinMessage());
        }
    }

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId, NetworkReader extraMessageReader)
    {
        var joinMessage = extraMessageReader.ReadMessage<JoinMessage>();
        var characterObject = Instantiate(GameInstance.Singleton.characterPrefab.gameObject);
        var character = characterObject.GetComponent<CharacterEntity>();
        character.Hp = character.TotalHp;
        character.playerName = joinMessage.playerName;
        character.selectHead = joinMessage.selectHead;
        character.selectCharacter = joinMessage.selectCharacter;
        character.selectWeapons = joinMessage.selectWeapons;
        GameplayManager.Singleton.characters.Add(character);
        NetworkServer.AddPlayerForConnection(conn, characterObject, playerControllerId);
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        var character = conn.playerControllers[0].gameObject.GetComponent<CharacterEntity>();
        GameplayManager.Singleton.characters.Remove(character);
        NetworkServer.DestroyPlayersForConnection(conn);
    }

    [System.Serializable]
    public class JoinMessage : MessageBase
    {
        public string playerName;
        public string selectHead;
        public string selectCharacter;
        public string selectWeapons;
    }
}
