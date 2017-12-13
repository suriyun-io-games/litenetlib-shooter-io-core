using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PowerUpEntity : NetworkBehaviour
{
    // We're going to respawn this power up so I decide to keep its prefab name to spawning when character triggered
    [HideInInspector]
    public string prefabName;
    [Header("Recovery / Stats")]
    public int hp;
    public int armor;
    public int exp;
    [Header("Effect")]
    public EffectEntity powerUpEffect;

    private bool isDead;
    private bool isPlayedEffects;

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
            if (!isPlayedEffects)
            {
                EffectEntity.PlayEffect(powerUpEffect, character.effectTransform);
                isPlayedEffects = true;
            }
            if (isServer)
            {
                character.Hp += Mathf.CeilToInt(hp * character.TotalHpRecoveryRate);
                character.Armor += Mathf.CeilToInt(armor * character.TotalArmorRecoveryRate);
                character.Exp += Mathf.CeilToInt(exp * character.TotalExpRate);
                // Destroy this on all clients
                NetworkServer.Destroy(gameObject);
                GameplayManager.Singleton.SpawnPowerUp(prefabName);
                isDead = true;
            }
        }
    }
}
