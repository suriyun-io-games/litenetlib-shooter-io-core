using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

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
    [HideInInspector, SyncVar(hook = "OnAttackerNetIdChanged")]
    public NetworkInstanceId attackerNetId;
    [SyncVar]
    public float addRotationX = 0;
    [SyncVar]
    public float addRotationY = 0;
    [HideInInspector]
    public int weaponDamage;
    private CharacterEntity attacker;
    public CharacterEntity Attacker
    {
        get
        {
            if (!isServer && attacker == null)
            {
                var go = ClientScene.FindLocalObject(attackerNetId);
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
            OnAttackerNetIdChanged(attackerNetId);
    }

    public override void OnStartServer()
    {
        StartCoroutine(NetworkDestroy(lifeTime));
    }

    private void OnAttackerNetIdChanged(NetworkInstanceId value)
    {
        attackerNetId = value;
        InitAttacker(Attacker);
    }

    /// <summary>
    /// This function will be called at client and server to set damage transform
    /// </summary>
    /// <param name="attacker"></param>
    private void InitAttacker(CharacterEntity attacker)
    {
        if (attacker == null)
            return;

        var damageLaunchTransform = attacker.damageLaunchTransform;
        if (relateToAttacker)
        {
            TempTransform.SetParent(damageLaunchTransform);
            var baseAngles = damageLaunchTransform.eulerAngles;
            TempTransform.rotation = Quaternion.Euler(baseAngles.x, baseAngles.y + addRotationY, baseAngles.z);
            TempTransform.position = damageLaunchTransform.position + TempTransform.forward * spawnForwardOffset;
        }
    }

    /// <summary>
    /// Init Attacker, this function must be call at server to init attacker
    /// </summary>
    /// <param name="addRotationY"></param>
    public void InitAttacker(CharacterEntity attacker, float addRotationX, float addRotationY)
    {
        if (attacker == null || !NetworkServer.active)
            return;

        this.attacker = attacker;
        this.addRotationX = addRotationX;
        this.addRotationY = addRotationY;

        attackerNetId = attacker.netId;
        InitAttacker(attacker);

        var damageLaunchTransform = attacker.damageLaunchTransform;
        var baseAngles = damageLaunchTransform.eulerAngles;
        TempTransform.rotation = Quaternion.Euler(baseAngles.x + addRotationX, baseAngles.y + addRotationY, baseAngles.z);
        TempTransform.position = damageLaunchTransform.position + TempTransform.forward * spawnForwardOffset;
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
                TempTransform.rotation = Quaternion.Euler(baseAngles.x + addRotationX, baseAngles.y + addRotationY, baseAngles.z);
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
        if (otherCharacter != null && otherCharacter.netId.Value == attackerNetId.Value)
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
            if (target == null || target == otherCharacter || target.netId.Value == attackerNetId.Value || target.Hp <= 0)
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
