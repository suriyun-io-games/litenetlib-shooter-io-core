using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class WeaponData : ItemData
{
    [Range(0, CharacterEntity.MAX_EQUIPPABLE_WEAPON_AMOUNT - 1)]
    public int equipPosition;
    public GameObject rightHandObject;
    public GameObject leftHandObject;
    public GameObject shieldObject;
    public List<AttackAnimation> attackAnimations;
    public DamageEntity damagePrefab;
    public int damage;
    [Header("Reload")]
    public bool reloadOneAmmoAtATime;
    public float reloadDuration;
    [Header("Ammo")]
    public bool unlimitAmmo;
    [Range(1, 999)]
    public int maxAmmo;
    [Range(1, 999)]
    public int maxReserveAmmo;
    [Range(1, 10)]
    public int spread;
    [Range(0, 100)]
    public float staggerX;
    [Range(0, 100)]
    public float staggerY;
    [Header("SFX")]
    public AudioClip[] attackFx;
    public AudioClip clipOutFx;
    public AudioClip clipInFx;
    public AudioClip emptyFx;
    public int weaponAnimId;
    public readonly Dictionary<int, AttackAnimation> AttackAnimations = new Dictionary<int, AttackAnimation>();

    public void Launch(CharacterEntity attacker, bool isLeftHandWeapon)
    {
        if (attacker == null || !NetworkServer.active)
            return;

        var characterColliders = Physics.OverlapSphere(attacker.TempTransform.position, damagePrefab.GetAttackRange() + 5f, 1 << GameInstance.Singleton.characterLayer);

        for (int i = 0; i < spread; ++i)
        {
            Transform launchTransform;
            attacker.GetDamageLaunchTransform(isLeftHandWeapon, out launchTransform);
            // An transform's rotation, position will be set when set `Attacker`
            // So don't worry about them before damage entity going to spawn
            // Velocity also being set when set `Attacker` too.
            var addRotationX = Random.Range(-staggerY, staggerY);
            var addRotationY = Random.Range(-staggerX, staggerX);
            var position = launchTransform.position;
            var direction = attacker.TempTransform.forward;
            var damageEntity = DamageEntity.InstantiateNewEntity(damagePrefab, isLeftHandWeapon, position, direction, attacker.netId, addRotationX, addRotationY);
            damageEntity.weaponDamage = Mathf.CeilToInt(damage / spread);
            var msg = new OpMsgCharacterAttack();
            msg.weaponId = GetId();
            msg.position = position;
            msg.direction = direction;
            msg.attackerNetId = attacker.netId;
            msg.addRotationX = addRotationX;
            msg.addRotationY = addRotationY;
            foreach (var characterCollider in characterColliders)
            {
                var character = characterCollider.GetComponent<CharacterEntity>();
                if (character != null && !(character is BotEntity))
                    NetworkServer.SendToClient(character.connectionToClient.connectionId, msg.OpId, msg);
            }
        }

        attacker.RpcEffect(attacker.netId, CharacterEntity.RPC_EFFECT_DAMAGE_SPAWN);
    }

    public void SetupAnimations()
    {
        foreach (var attackAnimation in attackAnimations)
        {
            AttackAnimations[attackAnimation.actionId] = attackAnimation;
        }
    }

    public AttackAnimation GetRandomAttackAnimation()
    {
        var list = AttackAnimations.Values.ToList();
        var randomedIndex = Random.Range(0, list.Count);
        return list[randomedIndex];
    }
}
