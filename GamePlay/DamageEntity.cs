﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;

[RequireComponent(typeof(Rigidbody))]
public class DamageEntity : MonoBehaviour
{
    public EffectEntity spawnEffectPrefab;
    public EffectEntity muzzleEffectPrefab;
    public EffectEntity explodeEffectPrefab;
    public EffectEntity hitEffectPrefab;
    public AudioClip[] hitFx;
    public float radius;
    public float explosionForceRadius;
    public float explosionForce;
    public float lifeTime;
    public float spawnForwardOffset;
    public float speed;
    public bool relateToAttacker;
    private bool isDead;
    private bool isLeftHandWeapon;
    private uint attackerNetId;
    private float addRotationX;
    private float addRotationY;
    private float? colliderExtents;
    [HideInInspector]
    public int weaponDamage;

    private CharacterEntity attacker;
    public CharacterEntity Attacker
    {
        get
        {
            if (attacker == null)
            {
                LiteNetLibIdentity go;
                if (GameNetworkManager.Singleton.Assets.TryGetSpawnedObject(attackerNetId, out go))
                    attacker = go.GetComponent<CharacterEntity>();
            }
            return attacker;
        }
    }
    public Transform CacheTransform { get; private set; }
    public Rigidbody CacheRigidbody { get; private set; }
    public Collider CacheCollider { get; private set; }

    private void Awake()
    {
        gameObject.layer = GenericUtils.IgnoreRaycastLayer;
        CacheTransform = transform;
        CacheRigidbody = GetComponent<Rigidbody>();
        CacheCollider = GetComponent<Collider>();
        CacheCollider.isTrigger = true;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// Init Attacker, this function must be call at server to init attacker
    /// </summary>
    public void InitAttackData(bool isLeftHandWeapon, uint attackerNetId, float addRotationX, float addRotationY)
    {
        this.isLeftHandWeapon = isLeftHandWeapon;
        this.attackerNetId = attackerNetId;
        this.addRotationX = addRotationX;
        this.addRotationY = addRotationY;
        InitTransform();
    }

    private void InitTransform()
    {
        if (Attacker == null)
            return;

        if (relateToAttacker)
        {
            Transform damageLaunchTransform;
            Attacker.GetDamageLaunchTransform(isLeftHandWeapon, out damageLaunchTransform);
            CacheTransform.SetParent(damageLaunchTransform);
            var baseAngles = attacker.CacheTransform.eulerAngles;
            CacheTransform.rotation = Quaternion.Euler(baseAngles.x + addRotationX, baseAngles.y + addRotationY, baseAngles.z);
        }
    }

    private void FixedUpdate()
    {
        UpdateMovement();
    }

    private void UpdateMovement()
    {
        if (Attacker != null)
        {
            if (relateToAttacker)
            {
                if (CacheTransform.parent == null)
                {
                    Transform damageLaunchTransform;
                    Attacker.GetDamageLaunchTransform(isLeftHandWeapon, out damageLaunchTransform);
                    CacheTransform.SetParent(damageLaunchTransform);
                }
                var baseAngles = attacker.CacheTransform.eulerAngles;
                CacheTransform.rotation = Quaternion.Euler(baseAngles.x + addRotationX, baseAngles.y + addRotationY, baseAngles.z);
            }
        }
        CacheRigidbody.velocity = GetForwardVelocity();
    }

    private void OnDestroy()
    {
        if (!isDead)
        {
            Explode(null);
            EffectEntity.PlayEffect(explodeEffectPrefab, CacheTransform);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == GenericUtils.IgnoreRaycastLayer)
            return;

        var otherCharacter = other.GetComponent<CharacterEntity>();
        // Damage will not hit attacker, so avoid it
        if (otherCharacter != null && otherCharacter.ObjectId == attackerNetId)
            return;

        var hitSomeAliveCharacter = false;
        if (otherCharacter != null &&
            otherCharacter.Hp > 0 &&
            !otherCharacter.isInvincible &&
            GameplayManager.Singleton.CanReceiveDamage(otherCharacter, attacker))
        {
            if (!otherCharacter.IsHidding)
                EffectEntity.PlayEffect(hitEffectPrefab, otherCharacter.effectTransform);
            ApplyDamage(otherCharacter);
            hitSomeAliveCharacter = true;
        }

        if (Explode(otherCharacter))
        {
            hitSomeAliveCharacter = true;
        }

        // If hit character (So it will not wall) but not hit alive character, don't destroy, let's find another target.
        if (otherCharacter != null && !hitSomeAliveCharacter)
            return;

        if (!isDead && hitSomeAliveCharacter)
        {
            // Play hit effect
            if (hitFx != null && hitFx.Length > 0 && AudioManager.Singleton != null)
                AudioSource.PlayClipAtPoint(hitFx[Random.Range(0, hitFx.Length - 1)], CacheTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
        }

        Destroy(gameObject);
        isDead = true;
    }

    private bool Explode(CharacterEntity otherCharacter)
    {
        var hitSomeAliveCharacter = false;
        Collider[] colliders = Physics.OverlapSphere(CacheTransform.position, radius, 1 << GameInstance.Singleton.characterLayer);
        CharacterEntity hitCharacter;
        for (int i = 0; i < colliders.Length; i++)
        {
            hitCharacter = colliders[i].GetComponent<CharacterEntity>();
            // If not character or character is attacker, skip it.
            if (hitCharacter == null ||
                hitCharacter == otherCharacter ||
                hitCharacter.ObjectId == attackerNetId ||
                hitCharacter.Hp <= 0 ||
                hitCharacter.isInvincible ||
                !GameplayManager.Singleton.CanReceiveDamage(hitCharacter, attacker))
                continue;
            if (!hitCharacter.IsHidding)
                EffectEntity.PlayEffect(hitEffectPrefab, hitCharacter.effectTransform);
            ApplyDamage(hitCharacter);
            hitSomeAliveCharacter = true;
        }
        return hitSomeAliveCharacter;
    }

    private void ApplyDamage(CharacterEntity target)
    {
        // Damage receiving calculation on server only
        if (GameNetworkManager.Singleton.IsServer && Attacker != null)
        {
            float damage = weaponDamage * Attacker.TotalWeaponDamageRate;
            damage += (Random.Range(GameplayManager.Singleton.minAttackVaryRate, GameplayManager.Singleton.maxAttackVaryRate) * damage);
            target.ReceiveDamage(Attacker, Mathf.CeilToInt(damage));
        }
        target.CacheCharacterMovement.AddExplosionForce(CacheTransform.position, explosionForce, explosionForceRadius);
    }

    private float GetColliderExtents()
    {
        if (colliderExtents.HasValue)
            return colliderExtents.Value;
        var tempObject = Instantiate(gameObject);
        var tempCollider = tempObject.GetComponent<Collider>();
        colliderExtents = Mathf.Min(tempCollider.bounds.extents.x, tempCollider.bounds.extents.z);
        Destroy(tempObject);
        return colliderExtents.Value;
    }

    public float GetAttackRange()
    {
        // s = v * t
        return (speed * lifeTime * GameplayManager.REAL_MOVE_SPEED_RATE) + GetColliderExtents();
    }

    public Vector3 GetForwardVelocity()
    {
        return CacheTransform.forward * speed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }

    public static DamageEntity InstantiateNewEntity(OpMsgCharacterAttack msg)
    {
        return InstantiateNewEntity(msg.weaponId, msg.isLeftHandWeapon, msg.targetPosition, msg.attackerNetId, msg.addRotationX, msg.addRotationY);
    }

    public static DamageEntity InstantiateNewEntity(
        int weaponId,
        bool isLeftHandWeapon,
        Vector3 targetPosition,
        uint attackerNetId,
        float addRotationX,
        float addRotationY)
    {
        WeaponData weaponData;
        if (GameInstance.Weapons.TryGetValue(weaponId, out weaponData))
            return InstantiateNewEntity(weaponData.damagePrefab, isLeftHandWeapon, targetPosition, attackerNetId, addRotationX, addRotationY);
        return null;
    }

    public static DamageEntity InstantiateNewEntity(
        DamageEntity prefab,
        bool isLeftHandWeapon,
        Vector3 targetPosition,
        uint attackerNetId,
        float addRotationX,
        float addRotationY)
    {
        if (prefab == null)
            return null;

        CharacterEntity attacker = null;
        LiteNetLibIdentity go;
        if (GameNetworkManager.Singleton.Assets.TryGetSpawnedObject(attackerNetId, out go))
            attacker = go.GetComponent<CharacterEntity>();

        if (attacker != null)
        {
            Transform launchTransform;
            attacker.GetDamageLaunchTransform(isLeftHandWeapon, out launchTransform);
            Vector3 position = launchTransform.position + attacker.CacheTransform.forward * prefab.spawnForwardOffset;
            Vector3 dir = targetPosition - position;
            Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);
            rotation = Quaternion.Euler(rotation.eulerAngles + new Vector3(addRotationX, addRotationY));
            DamageEntity result = Instantiate(prefab, position, rotation);
            result.InitAttackData(isLeftHandWeapon, attackerNetId, addRotationX, addRotationY);
            return result;
        }
        return null;
    }
}
