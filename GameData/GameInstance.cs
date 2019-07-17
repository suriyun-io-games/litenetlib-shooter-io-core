using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameInstance : BaseNetworkGameInstance
{
    public static new GameInstance Singleton { get; private set; }
    public CharacterEntity characterPrefab;
    public BotEntity botPrefab;
    public CharacterData[] characters;
    public HeadData[] heads;
    public WeaponData[] weapons;
    public CustomEquipmentData[] customEquipments;
    public BotData[] bots;
    [Tooltip("Physic layer for characters to avoid it collision")]
    public int characterLayer = 8;
    public bool showJoystickInEditor = true;
    public string watchAdsRespawnPlacement = "respawnPlacement";
    // An available list, list of item that already unlocked
    public static readonly List<HeadData> AvailableHeads = new List<HeadData>();
    public static readonly List<CharacterData> AvailableCharacters = new List<CharacterData>();
    public static readonly List<WeaponData> AvailableWeapons = new List<WeaponData>();
    public static readonly List<CustomEquipmentData> AvailableCustomEquipments = new List<CustomEquipmentData>();
    // All item list
    public static readonly Dictionary<int, HeadData> Heads = new Dictionary<int, HeadData>();
    public static readonly Dictionary<int, CharacterData> Characters = new Dictionary<int, CharacterData>();
    public static readonly Dictionary<int, WeaponData> Weapons = new Dictionary<int, WeaponData>();
    public static readonly Dictionary<int, CustomEquipmentData> CustomEquipments = new Dictionary<int, CustomEquipmentData>();
    protected override void Awake()
    {
        base.Awake();
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;
        DontDestroyOnLoad(gameObject);
        Physics.IgnoreLayerCollision(characterLayer, characterLayer, true);

        Heads.Clear();
        foreach (var head in heads)
        {
            Heads.Add(head.GetHashId(), head);
        }

        Characters.Clear();
        foreach (var character in characters)
        {
            Characters.Add(character.GetHashId(), character);
        }

        Weapons.Clear();
        foreach (var weapon in weapons)
        {
            weapon.SetupAnimations();
            Weapons.Add(weapon.GetHashId(), weapon);
        }

        CustomEquipments.Clear();
        foreach (var customEquipment in customEquipments)
        {
            CustomEquipments[customEquipment.GetHashId()] = customEquipment;
        }
    }

    protected override void Start()
    {
        base.Start();
        UpdateAvailableItems();
        ValidatePlayerSave();
    }

    public void UpdateAvailableItems()
    {
        AvailableHeads.Clear();
        foreach (var helmet in heads)
        {
            if (helmet != null && helmet.IsUnlock())
                AvailableHeads.Add(helmet);
        }

        AvailableCharacters.Clear();
        foreach (var character in characters)
        {
            if (character != null && character.IsUnlock())
                AvailableCharacters.Add(character);
        }

        AvailableWeapons.Clear();
        foreach (var weapon in weapons)
        {
            if (weapon != null && weapon.IsUnlock())
                AvailableWeapons.Add(weapon);
        }

        AvailableCustomEquipments.Clear();
        foreach (var customEquipment in customEquipments)
        {
            if (customEquipment != null && customEquipment.IsUnlock())
                AvailableCustomEquipments.Add(customEquipment);
        }
    }

    public void ValidatePlayerSave()
    {
        var head = PlayerSave.GetHead();
        if (head < 0 || head >= AvailableHeads.Count)
            PlayerSave.SetHead(0);

        var character = PlayerSave.GetCharacter();
        if (character < 0 || character >= AvailableCharacters.Count)
            PlayerSave.SetCharacter(0);

        // Initial first player save
        var savedWeapons = PlayerSave.GetWeapons();
        if (savedWeapons.Count == 0)
        {
            var savingWeapons = new Dictionary<int, int>();
            for (var i = 0; i < AvailableWeapons.Count; ++i)
            {
                var savingWeapon = AvailableWeapons[i];
                var equipPosition = savingWeapon.equipPosition;
                if (!savingWeapons.ContainsKey(equipPosition))
                    savingWeapons[equipPosition] = i;
            }
            PlayerSave.SetWeapons(savingWeapons);
        }
        else
        {
            var savingWeapons = new Dictionary<int, int>();
            foreach (var savedWeapon in savedWeapons)
            {
                var equippedPosition = savedWeapon.Key;
                var availableIndex = savedWeapon.Value;
                if (availableIndex >= 0 && availableIndex < AvailableWeapons.Count)
                {
                    var savingWeapon = AvailableWeapons[availableIndex];
                    var equipPosition = savingWeapon.equipPosition;
                    if (!savingWeapons.ContainsKey(equipPosition))
                        savingWeapons[equipPosition] = availableIndex;
                }
            }
            PlayerSave.SetWeapons(savingWeapons);
        }
    }

    public static HeadData GetHead(int key)
    {
        if (Heads.Count == 0)
            return null;
        HeadData result;
        Heads.TryGetValue(key, out result);
        return result;
    }

    public static CharacterData GetCharacter(int key)
    {
        if (Characters.Count == 0)
            return null;
        CharacterData result;
        Characters.TryGetValue(key, out result);
        return result;
    }

    public static WeaponData GetWeapon(int key)
    {
        if (Weapons.Count == 0)
            return null;
        WeaponData result;
        Weapons.TryGetValue(key, out result);
        return result;
    }

    public static CustomEquipmentData GetCustomEquipment(int key)
    {
        if (CustomEquipments.Count == 0)
            return null;
        CustomEquipmentData result;
        CustomEquipments.TryGetValue(key, out result);
        return result;
    }

    public static HeadData GetAvailableHead(int index)
    {
        if (AvailableHeads.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableHeads.Count)
            index = 0;
        return AvailableHeads[index];
    }

    public static CharacterData GetAvailableCharacter(int index)
    {
        if (AvailableCharacters.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableCharacters.Count)
            index = 0;
        return AvailableCharacters[index];
    }

    public static WeaponData GetAvailableWeapon(int index)
    {
        if (AvailableWeapons.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableWeapons.Count)
            index = 0;
        return AvailableWeapons[index];
    }

    public static CustomEquipmentData GetAvailableCustomEquipment(int index)
    {
        if (AvailableCustomEquipments.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableCustomEquipments.Count)
            index = 0;
        return AvailableCustomEquipments[index];
    }
}
