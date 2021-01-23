using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;
using LiteNetLib.Utils;

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
        var headData = GameInstance.GetAvailableHead(PlayerSave.GetHead());
        var characterData = GameInstance.GetAvailableCharacter(PlayerSave.GetCharacter());
        msg.selectHead = headData != null ? headData.GetHashId() : 0;
        msg.selectCharacter = characterData != null ? characterData.GetHashId() : 0;
        // Weapons
        var savedWeapons = PlayerSave.GetWeapons();
        var selectWeapons = new List<int>();
        foreach (var savedWeapon in savedWeapons.Values)
        {
            var data = GameInstance.GetAvailableWeapon(savedWeapon);
            if (data != null)
                selectWeapons.Add(data.GetHashId());
        }
        msg.selectWeapons = selectWeapons.ToArray();
        // Custom Equipments
        var savedCustomEquipments = PlayerSave.GetCustomEquipments();
        var selectCustomEquipments = new List<int>();
        foreach (var savedCustomEquipment in savedCustomEquipments)
        {
            var data = GameInstance.GetAvailableCustomEquipment(savedCustomEquipment.Value);
            if (data != null)
                selectCustomEquipments.Add(data.GetHashId());
        }
        msg.selectCustomEquipments = selectCustomEquipments.ToArray();
        return msg;
    }

    protected override void RegisterMessages()
    {
        base.RegisterMessages();
        RegisterClientMessage(new OpMsgCharacterAttack().OpId, ReadMsgCharacterAttack);
    }

    protected void ReadMsgCharacterAttack(MessageHandlerData messageHandler)
    {
        var msg = messageHandler.ReadMessage<OpMsgCharacterAttack>();
        // Instantiates damage entities on clients only
        if (!IsServer)
            DamageEntity.InstantiateNewEntity(msg);
    }

    protected override void PrepareCharacter(NetDataWriter writer)
    {
        MakeJoinMessage().Serialize(writer);
    }

    protected override BaseNetworkGameCharacter NewCharacter(NetDataReader reader)
    {
        var joinMessage = new JoinMessage();
        joinMessage.Deserialize(reader);
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
        foreach (var weapon in joinMessage.selectWeapons)
        {
            character.selectWeapons.Add(weapon);
        }
        foreach (var customEquipment in joinMessage.selectCustomEquipments)
        {
            character.selectCustomEquipments.Add(customEquipment);
        }
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
            if (BaseNetworkGameCharacter.Local != null && score.netId.Equals(BaseNetworkGameCharacter.Local.ObjectId))
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
    public class JoinMessage : INetSerializable
    {
        public string playerName;
        public int selectHead;
        public int selectCharacter;
        public int[] selectWeapons;
        public int[] selectCustomEquipments;
        public string extra;

        public void Deserialize(NetDataReader reader)
        {
            playerName = reader.GetString();
            selectHead = reader.GetInt();
            selectCharacter = reader.GetInt();
            selectWeapons = reader.GetIntArray();
            selectCustomEquipments = reader.GetIntArray();
            extra = reader.GetString();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerName);
            writer.Put(selectHead);
            writer.Put(selectCharacter);
            writer.PutArray(selectWeapons);
            writer.PutArray(selectCustomEquipments);
            writer.Put(extra);
        }
    }
}
