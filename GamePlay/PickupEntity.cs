using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PickupEntity : NetworkBehaviour
{
    public enum PickupType
    {
        Weapon,
        Ammo
    }
    // We're going to respawn this item so I decide to keep its prefab name to spawning when character triggered
    [HideInInspector]
    public string prefabName;
    public Texture iconTexture;
    public PickupType type;
    public WeaponData weaponData;
    public int ammoAmount;
    private bool isDead;

    private void Awake()
    {
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        var character = other.GetComponent<CharacterEntity>();
        if (character != null && character.Hp > 0)
        {
            if (isServer)
            {
                bool isPickedup = false;
                switch (type)
                {
                    case PickupType.Weapon:
                        isPickedup = character.ServerChangeEquippedWeapon(weaponData, ammoAmount);
                        break;
                    case PickupType.Ammo:
                        isPickedup = character.ServerFillWeaponAmmo(weaponData, ammoAmount);
                        break;
                }
                // Destroy this on all clients
                if (isPickedup)
                {
                    isDead = true;
                    NetworkServer.Destroy(gameObject);
                    GameplayManager.Singleton.SpawnPickup(prefabName);
                }
            }
        }
    }
}
