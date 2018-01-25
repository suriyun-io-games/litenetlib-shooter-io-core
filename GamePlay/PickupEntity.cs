using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PickupEntity : NetworkBehaviour
{
    public enum PickupType
    {
        WeaponOrEquipment,
        Ammo,
    }
    // We're going to respawn this item so I decide to keep its prefab name to spawning when character triggered
    [HideInInspector]
    public string prefabName;
    public PickupType type;
    [System.Obsolete]
    public WeaponData weaponData;
    public BasePickupItemData itemData;
    public int ammoAmount;
    private bool isDead;

    public Texture IconTexture
    {
        get { return itemData.iconTexture; }
    }

    public Texture PreviewTexture
    {
        get { return itemData.previewTexture; }
    }

    public string Title
    {
        get { return itemData.GetTitle(); }
    }

    public string Description
    {
        get { return itemData.GetDescription(); }
    }

    private void Awake()
    {
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (itemData == null && weaponData != null)
            itemData = weaponData;
        weaponData = null;
        EditorUtility.SetDirty(this);
    }
#endif

    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        var gameplayManager = GameplayManager.Singleton;
        var character = other.GetComponent<CharacterEntity>();
        if (character != null && character.Hp > 0)
        {
            if (isServer && gameplayManager.autoPickup)
            {
                bool isPickedup = false;
                switch (type)
                {
                    case PickupType.WeaponOrEquipment:
                        if (itemData is EquipmentData)
                            isPickedup = character.ServerChangeSelectEquipment(itemData as EquipmentData);
                        if (itemData is WeaponData)
                            isPickedup = character.ServerChangeSelectWeapon(itemData as WeaponData, ammoAmount);
                        break;
                    case PickupType.Ammo:
                        if (itemData is WeaponData)
                            isPickedup = character.ServerFillWeaponAmmo(itemData as WeaponData, ammoAmount);
                        break;
                }
                // Destroy this on all clients
                if (isPickedup)
                {
                    isDead = true;
                    NetworkServer.Destroy(gameObject);
                    gameplayManager.SpawnPickup(prefabName);
                }
            }

            if (!gameplayManager.autoPickup && character.isLocalPlayer)
            {
                if (!character.PickableEntities.ContainsKey(netId.Value))
                    character.PickableEntities[netId.Value] = this;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isDead)
            return;

        var gameplayManager = GameplayManager.Singleton;
        var character = other.GetComponent<CharacterEntity>();
        if (character != null && character.Hp > 0)
        {
            if (!gameplayManager.autoPickup && character.isLocalPlayer)
            {
                if (character.PickableEntities.ContainsKey(netId.Value))
                    character.PickableEntities.Remove(netId.Value);
            }
        }
    }
}
