using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public struct DamageAttackData
{
    public NetworkInstanceId netId;
    public float addRotationX;
    public float addRotationY;
    public static DamageAttackData Create(NetworkInstanceId attackerNetId, float addRotationX, float addRotationY)
    {
        var result = new DamageAttackData();
        result.netId = attackerNetId;
        result.addRotationX = addRotationX;
        result.addRotationY = addRotationY;
        return result;
    }
}

[RequireComponent(typeof(Rigidbody))]
public class DamageEntity : NetworkBehaviour
{
    public EffectEntity spawnEffectPrefab;
    public EffectEntity explodeEffectPrefab;
    public EffectEntity hitEffectPrefab;
    public AudioClip[] hitFx;
    public float radius;
    public float lifeTime;
    public float spawnForwardOffset;
    public float speed;
    public bool relateToAttacker;
    private bool isDead;
    /// <summary>
    /// We use this `attacketNetId` to let clients able to find `attacker` entity,
    /// This should be called only once when it spawn to reduce networking works
    /// </summary>
    [HideInInspector, SyncVar(hook = "OnDamageData")]
    public DamageAttackData damageAttacker;
    [HideInInspector]
    public int weaponDamage;
    private CharacterEntity attacker;
    public CharacterEntity Attacker
    {
        get
        {
            if (!isServer && attacker == null)
            {
                var go = ClientScene.FindLocalObject(damageAttacker.netId);
                if (go != null)
                    attacker = go.GetComponent<CharacterEntity>();
            }
            return attacker;
        }
    }

    private Transform tempTransform;
    public Transform TempTransform
    {
        get
        {
            if (tempTransform == null)
                tempTransform = GetComponent<Transform>();
            return tempTransform;
        }
    }
    private Rigidbody tempRigidbody;
    public Rigidbody TempRigidbody
    {
        get
        {
            if (tempRigidbody == null)
                tempRigidbody = GetComponent<Rigidbody>();
            return tempRigidbody;
        }
    }

    private void Awake()
    {
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;
    }

    public override void OnStartClient()
    {
        if (!isServer)
            OnDamageData(damageAttacker);
    }

    public override void OnStartServer()
    {
        StartCoroutine(NetworkDestroy(lifeTime));
    }

    private void OnDamageData(DamageAttackData value)
    {
        damageAttacker = value;
        InitTransform();
    }

    private void InitTransform()
    {
        if (attacker == null)
            return;
        var damageLaunchTransform = attacker.damageLaunchTransform;
        if (relateToAttacker)
            TempTransform.SetParent(damageLaunchTransform);
        var baseAngles = damageLaunchTransform.eulerAngles;
        TempTransform.rotation = Quaternion.Euler(baseAngles.x + damageAttacker.addRotationX, baseAngles.y + damageAttacker.addRotationY, baseAngles.z);
        TempTransform.position = damageLaunchTransform.position + TempTransform.forward * spawnForwardOffset;
    }

    /// <summary>
    /// Init Attacker, this function must be call at server to init attacker
    /// </summary>
    public void InitAttacker(CharacterEntity attacker, float addRotationX, float addRotationY)
    {
        if (attacker == null || !NetworkServer.active)
            return;

        this.attacker = attacker;
        damageAttacker = DamageAttackData.Create(attacker.netId, addRotationX, addRotationY);
        InitTransform();
    }

    private void FixedUpdate()
    {
        UpdateMovement();
    }

    private void UpdateMovement()
    {
        var attacker = Attacker;
        if (attacker != null)
        {
            if (relateToAttacker)
            {
                var baseAngles = attacker.damageLaunchTransform.eulerAngles;
                TempTransform.rotation = Quaternion.Euler(baseAngles.x + damageAttacker.addRotationX, baseAngles.y + damageAttacker.addRotationY, baseAngles.z);
                TempRigidbody.velocity = attacker.TempRigidbody.velocity + GetForwardVelocity();
            }
            else
                TempRigidbody.velocity = GetForwardVelocity();
        }
        else
            TempRigidbody.velocity = GetForwardVelocity();
    }

    IEnumerator NetworkDestroy(float time)
    {
        if (time < 0)
            time = 0;
        yield return new WaitForSecondsRealtime(time);
        NetworkServer.Destroy(gameObject);
    }

    public override void OnNetworkDestroy()
    {
        if (!isDead)
            EffectEntity.PlayEffect(explodeEffectPrefab, TempTransform);
        base.OnNetworkDestroy();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PowerUpEntity>() != null || other.GetComponent<PickupEntity>() != null || other.GetComponent<DamageEntity>())
            return;

        var otherCharacter = other.GetComponent<CharacterEntity>();
        // Damage will not hit attacker, so avoid it
        if (otherCharacter != null && otherCharacter.netId.Value == damageAttacker.netId.Value)
            return;

        var hitSomeAliveCharacter = false;
        if (otherCharacter != null && otherCharacter.Hp > 0)
        {
            hitSomeAliveCharacter = true;
            ApplyDamage(otherCharacter);
        }
        
        Collider[] colliders = Physics.OverlapSphere(TempTransform.position, radius, 1 << GameInstance.Singleton.characterLayer);
        for (int i = 0; i < colliders.Length; i++)
        {
            var target = colliders[i].GetComponent<CharacterEntity>();
            // If not character or character is attacker, skip it.
            if (target == null || target == otherCharacter || target.netId.Value == damageAttacker.netId.Value || target.Hp <= 0)
                continue;

            hitSomeAliveCharacter = true;
            ApplyDamage(target);
        }
        // If hit character (So it will not wall) but not hit alive character, don't destroy, let's find another target.
        if (otherCharacter != null && !hitSomeAliveCharacter)
            return;

        if (!isDead && hitSomeAliveCharacter)
        {
            // Play hit effect
            if (hitFx != null && hitFx.Length > 0 && AudioManager.Singleton != null)
                AudioSource.PlayClipAtPoint(hitFx[Random.Range(0, hitFx.Length - 1)], TempTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
        }

        // Destroy this on all clients
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
            isDead = true;
        }
        else if (!isDead)
        {
            EffectEntity.PlayEffect(explodeEffectPrefab, TempTransform);
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = false;
            }
            isDead = true;
        }
    }

    private void ApplyDamage(CharacterEntity target)
    {
        // Damage receiving calculation on server only
        if (isServer)
        {
            var gameplayManager = GameplayManager.Singleton;
            float damage = weaponDamage * Attacker.TotalWeaponDamageRate;
            damage += (Random.Range(gameplayManager.minAttackVaryRate, gameplayManager.maxAttackVaryRate) * damage);
            target.ReceiveDamage(Attacker, Mathf.CeilToInt(damage));
        }
    }

    public float GetAttackRange()
    {
        // s = v * t
        return (TempRigidbody.velocity.magnitude * lifeTime) + (radius / 2);
    }

    public Vector3 GetForwardVelocity()
    {
        return TempTransform.forward * speed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }
}
