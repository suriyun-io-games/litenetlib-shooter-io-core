using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class CharacterEntity : NetworkBehaviour, System.IComparable<CharacterEntity>
{
    public const byte RPC_EFFECT_DAMAGE_SPAWN = 0;
    public const byte RPC_EFFECT_DAMAGE_HIT = 1;
    public const byte RPC_EFFECT_TRAP_HIT = 2;
    public const int MAX_EQUIPPABLE_WEAPON_AMOUNT = 10;
    public static CharacterEntity Local { get; private set; }
    public Transform damageLaunchTransform;
    public Transform effectTransform;
    public Transform characterModelTransform;
    [Header("UI")]
    public Transform hpBarContainer;
    public Image hpFillImage;
    public Text hpText;
    public Image armorFillImage;
    public Text armorText;
    public Text nameText;
    [Header("Effect")]
    public GameObject invincibleEffect;
    [Header("Online data")]
    [SyncVar]
    public string playerName;

    [SyncVar]
    public int score;
    public int Score
    {
        get { return score; }
        set
        {
            if (!isServer)
                return;

            score = value;
            GameplayManager.Singleton.UpdateRank(netId);
        }
    }

    [SyncVar]
    public int hp;
    public int Hp
    {
        get { return hp; }
        set
        {
            if (!isServer)
                return;

            if (value <= 0)
            {
                value = 0;
                if (!isDead)
                {
                    if (connectionToClient != null)
                        TargetDead(connectionToClient);
                    deathTime = Time.unscaledTime;
                    isDead = true;
                }
            }
            if (value > TotalHp)
                value = TotalHp;
            hp = value;
        }
    }

    [SyncVar]
    public int armor;
    public int Armor
    {
        get { return armor; }
        set
        {
            if (!isServer)
                return;

            if (value <= 0)
                value = 0;

            if (value > TotalArmor)
                value = TotalArmor;
            armor = value;
        }
    }

    [SyncVar]
    public int exp;
    public int Exp
    {
        get { return exp; }
        set
        {
            if (!isServer)
                return;

            var gameplayManager = GameplayManager.Singleton;
            while (true)
            {
                if (level == gameplayManager.maxLevel)
                    break;

                var currentExp = gameplayManager.GetExp(level);
                if (value < currentExp)
                    break;
                var remainExp = value - currentExp;
                value = remainExp;
                ++level;
                statPoint += gameplayManager.addingStatPoint;
            }
            exp = value;
        }
    }

    [SyncVar]
    public int level = 1;

    [SyncVar]
    public int statPoint;

    [SyncVar]
    public int killCount;

    [SyncVar]
    public int watchAdsCount;

    [SyncVar(hook = "OnCharacterChanged")]
    public string selectCharacter = "";

    [SyncVar(hook = "OnHeadChanged")]
    public string selectHead = "";

    [SyncVar(hook = "OnWeaponsChanged")]
    public string selectWeapons = "";

    [SyncVar(hook = "OnWeaponChanged")]
    public int selectWeaponIndex = -1;

    [SyncVar]
    public bool isInvincible;

    [SyncVar, Tooltip("If this value >= 0 it's means character is attacking, so set it to -1 to stop attacks")]
    public int attackingActionId;

    [SyncVar]
    public CharacterStats addStats;

    public SyncListEquippedWeapon equippedWeapons = new SyncListEquippedWeapon();

    protected Coroutine attackRoutine;
    protected Coroutine reloadRoutine;
    protected Camera targetCamera;
    protected CharacterModel characterModel;
    protected CharacterData characterData;
    protected HeadData headData;
    protected int defaultWeaponIndex = -1;
    public float startReloadTime { get; private set; }
    public float reloadDuration { get; private set; }

    public bool isReady { get; private set; }
    public bool isDead { get; private set; }
    public bool isPlayingAttackAnim { get; private set; }
    public bool isReloading { get; private set; }
    public bool hasAttackInterruptReload { get; private set; }
    public float deathTime { get; private set; }
    public float invincibleTime { get; private set; }

    public float FinishReloadTimeRate
    {
        get { return (Time.unscaledTime - startReloadTime) / reloadDuration; }
    }

    public EquippedWeapon CurrentEquippedWeapon
    {
        get
        {
            try
            { return equippedWeapons[selectWeaponIndex]; }
            catch
            { return EquippedWeapon.Empty; }
        }
    }

    public WeaponData WeaponData
    {
        get
        {
            try
            { return CurrentEquippedWeapon.WeaponData; }
            catch
            { return null; }
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

    public int TotalHp
    {
        get
        {
            var total = GameplayManager.Singleton.baseMaxHp + addStats.addMaxHp;
            if (headData != null)
                total += headData.stats.addMaxHp;
            if (characterData != null)
                total += characterData.stats.addMaxHp;
            if (WeaponData != null)
                total += WeaponData.stats.addMaxHp;
            return total;
        }
    }

    public int TotalArmor
    {
        get
        {
            var total = GameplayManager.Singleton.baseMaxArmor + addStats.addMaxArmor;
            if (headData != null)
                total += headData.stats.addMaxArmor;
            if (characterData != null)
                total += characterData.stats.addMaxArmor;
            if (WeaponData != null)
                total += WeaponData.stats.addMaxArmor;
            return total;
        }
    }
    
    public int TotalMoveSpeed
    {
        get
        {
            var total = GameplayManager.Singleton.baseMoveSpeed + addStats.addMoveSpeed;
            if (headData != null)
                total += headData.stats.addMoveSpeed;
            if (characterData != null)
                total += characterData.stats.addMoveSpeed;
            if (WeaponData != null)
                total += WeaponData.stats.addMoveSpeed;
            return total;
        }
    }

    public float TotalWeaponDamageRate
    {
        get
        {
            var total = GameplayManager.Singleton.baseWeaponDamageRate + addStats.addWeaponDamageRate;
            if (headData != null)
                total += headData.stats.addWeaponDamageRate;
            if (characterData != null)
                total += characterData.stats.addWeaponDamageRate;
            if (WeaponData != null)
                total += WeaponData.stats.addWeaponDamageRate;
            return total;
        }
    }

    public float TotalArmorReduceDamage
    {
        get
        {
            var total = GameplayManager.Singleton.baseArmorReduceDamage + addStats.addArmorReduceDamage;
            if (headData != null)
                total += headData.stats.addArmorReduceDamage;
            if (characterData != null)
                total += characterData.stats.addArmorReduceDamage;
            if (WeaponData != null)
                total += WeaponData.stats.addArmorReduceDamage;
            return total;
        }
    }

    public float TotalExpRate
    {
        get
        {
            var total = 1 + addStats.addExpRate;
            if (headData != null)
                total += headData.stats.addExpRate;
            if (characterData != null)
                total += characterData.stats.addExpRate;
            if (WeaponData != null)
                total += WeaponData.stats.addExpRate;
            return total;
        }
    }

    public float TotalScoreRate
    {
        get
        {
            var total = 1 + addStats.addScoreRate;
            if (headData != null)
                total += headData.stats.addScoreRate;
            if (characterData != null)
                total += characterData.stats.addScoreRate;
            if (WeaponData != null)
                total += WeaponData.stats.addScoreRate;
            return total;
        }
    }

    public float TotalHpRecoveryRate
    {
        get
        {
            var total = 1 + addStats.addHpRecoveryRate;
            if (headData != null)
                total += headData.stats.addHpRecoveryRate;
            if (characterData != null)
                total += characterData.stats.addHpRecoveryRate;
            if (WeaponData != null)
                total += WeaponData.stats.addHpRecoveryRate;
            return total;
        }
    }

    public float TotalArmorRecoveryRate
    {
        get
        {
            var total = 1 + addStats.addArmorRecoveryRate;
            if (headData != null)
                total += headData.stats.addArmorRecoveryRate;
            if (characterData != null)
                total += characterData.stats.addArmorRecoveryRate;
            if (WeaponData != null)
                total += WeaponData.stats.addArmorRecoveryRate;
            return total;
        }
    }

    public float TotalDamageRateLeechHp
    {
        get
        {
            var total = addStats.addDamageRateLeechHp;
            if (headData != null)
                total += headData.stats.addDamageRateLeechHp;
            if (characterData != null)
                total += characterData.stats.addDamageRateLeechHp;
            if (WeaponData != null)
                total += WeaponData.stats.addDamageRateLeechHp;
            return total;
        }
    }

    private void Awake()
    {
        gameObject.layer = GameInstance.Singleton.characterLayer;
        if (damageLaunchTransform == null)
            damageLaunchTransform = TempTransform;
        if (effectTransform == null)
            effectTransform = TempTransform;
        if (characterModelTransform == null)
            characterModelTransform = TempTransform;
    }

    public override void OnStartClient()
    {
        if (!isServer)
        {
            OnHeadChanged(selectHead);
            OnCharacterChanged(selectCharacter);
            OnWeaponsChanged(selectWeapons);
            OnWeaponChanged(selectWeaponIndex);
        }
    }

    public override void OnStartServer()
    {
        for (var i = 0; i < MAX_EQUIPPABLE_WEAPON_AMOUNT; ++i)
            equippedWeapons.Add(EquippedWeapon.Empty);
        OnHeadChanged(selectHead);
        OnCharacterChanged(selectCharacter);
        OnWeaponsChanged(selectWeapons);
        OnWeaponChanged(selectWeaponIndex);
        attackingActionId = -1;
    }

    public override void OnStartLocalPlayer()
    {
        if (Local != null)
            return;

        Local = this;
        var followCam = FindObjectOfType<FollowCamera>();
        followCam.target = TempTransform;
        targetCamera = followCam.GetComponent<Camera>();
        GameplayManager.Singleton.uiGameplay.FadeOut();

        CmdReady();
    }

    private void Update()
    {
        if (isServer && isInvincible && Time.unscaledTime - invincibleTime >= GameplayManager.Singleton.invincibleDuration)
            isInvincible = false;
        if (invincibleEffect != null)
            invincibleEffect.SetActive(isInvincible);
        if (nameText != null)
            nameText.text = playerName;
        if (hpBarContainer != null)
            hpBarContainer.gameObject.SetActive(hp > 0);
        if (hpFillImage != null)
            hpFillImage.fillAmount = (float)hp / (float)TotalHp;
        if (hpText != null)
            hpText.text = hp + "/" + TotalHp;
        UpdateAnimation();

        // Update button inputs
        if (isLocalPlayer && hp > 0)
        {
            if (InputManager.GetButtonDown("Reload"))
                Reload();
            if (GameplayManager.Singleton.autoReload &&
                CurrentEquippedWeapon.currentAmmo == 0 &&
                CurrentEquippedWeapon.CanReload())
                Reload();
        }
    }

    private void FixedUpdate()
    {
        UpdateMovements();
    }

    protected virtual void UpdateAnimation()
    {
        if (characterModel == null)
            return;
        var animator = characterModel.TempAnimator;
        if (animator == null)
            return;
        if (Hp <= 0)
        {
            animator.SetBool("IsDead", true);
            animator.SetFloat("JumpSpeed", 0);
            animator.SetFloat("MoveSpeed", 0);
            animator.SetBool("IsGround", true);
        }
        else
        {
            var velocity = TempRigidbody.velocity;
            var xzMagnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
            var ySpeed = velocity.y;
            animator.SetBool("IsDead", false);
            animator.SetFloat("JumpSpeed", ySpeed);
            animator.SetFloat("MoveSpeed", xzMagnitude);
            animator.SetBool("IsGround", Mathf.Abs(ySpeed) < 0.5f);
        }

        if (attackingActionId >= 0 && !isPlayingAttackAnim)
            StartCoroutine(AttackRoutine(attackingActionId));
    }

    protected virtual void UpdateMovements()
    {
        if (!isLocalPlayer)
            return;

        if (Hp <= 0)
        {
            TempRigidbody.velocity = Vector3.zero;
            return;
        }

        var direction = new Vector3(InputManager.GetAxis("Horizontal", false), 0, InputManager.GetAxis("Vertical", false));
        if (direction.magnitude != 0)
        {
            if (direction.magnitude > 1)
                direction = direction.normalized;
            Vector3 movementDir = direction * TotalMoveSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
            TempRigidbody.velocity = movementDir;
        }
        else
            TempRigidbody.velocity = Vector3.zero;

        if (Application.isMobilePlatform)
        {
            direction = new Vector2(InputManager.GetAxis("Mouse X", false), InputManager.GetAxis("Mouse Y", false));
            Rotate(direction);
            if (direction.magnitude != 0)
                Attack();
            else
                StopAttack();
        }
        else
        {
            direction = (Input.mousePosition - targetCamera.WorldToScreenPoint(transform.position)).normalized;
            Rotate(direction);
            if (Input.GetMouseButton(0))
                Attack();
            else
                StopAttack();
        }
    }

    protected void Rotate(Vector2 direction)
    {
        if (direction.magnitude != 0)
        {
            int newRotation = (int)(Quaternion.LookRotation(new Vector3(direction.x, 0, direction.y)).eulerAngles.y + targetCamera.transform.eulerAngles.y);
            Quaternion targetRotation = Quaternion.Euler(0, newRotation, 0);
            TempTransform.rotation = Quaternion.Lerp(TempTransform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    protected void Attack()
    {
        if (isLocalPlayer)
        {
            // If attacking while reloading, determines that it is reload interrupting
            if (isReloading && FinishReloadTimeRate > 0.8f)
                hasAttackInterruptReload = true;
        }
        if (isPlayingAttackAnim || isReloading || !CurrentEquippedWeapon.CanShoot())
            return;
        if (attackingActionId < 0 && isLocalPlayer)
            CmdAttack();
    }

    protected void StopAttack()
    {
        if (attackingActionId >= 0 && isLocalPlayer)
            CmdStopAttack();
    }

    protected void Reload()
    {
        if (isPlayingAttackAnim || isReloading || !CurrentEquippedWeapon.CanReload())
            return;
        if (isLocalPlayer)
            CmdReload();
    }

    IEnumerator AttackRoutine(int actionId)
    {
        if (!isPlayingAttackAnim && !isReloading && CurrentEquippedWeapon.CanShoot() && Hp > 0)
        {
            isPlayingAttackAnim = true;
            if (WeaponData != null && characterModel != null)
            {
                var animator = characterModel.TempAnimator;
                if (animator != null)
                {
                    // Play attack animation
                    var attackAnimation = WeaponData.AttackAnimations[actionId];
                    animator.SetBool("DoAction", true);
                    animator.SetInteger("ActionID", attackAnimation.actionId);
                    var animationDuration = attackAnimation.animationDuration;
                    var launchDuration = attackAnimation.launchDuration;
                    if (launchDuration > animationDuration)
                        launchDuration = animationDuration;
                    yield return new WaitForSeconds(launchDuration);
                    // Launch damage entity on server only
                    if (isServer)
                    {
                        WeaponData.Launch(this);
                        var equippedWeapon = CurrentEquippedWeapon;
                        equippedWeapon.DecreaseAmmo();
                        equippedWeapons[selectWeaponIndex] = equippedWeapon;
                        equippedWeapons.Dirty(selectWeaponIndex);
                    }
                    if (WeaponData.attackFx != null && WeaponData.attackFx.Length > 0 && AudioManager.Singleton != null)
                        AudioSource.PlayClipAtPoint(WeaponData.attackFx[Random.Range(0, WeaponData.attackFx.Length - 1)], TempTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
                    yield return new WaitForSeconds(animationDuration - launchDuration);
                    // Attack animation ended
                    animator.SetBool("DoAction", false);
                }
            }
            yield return new WaitForEndOfFrame();
            isPlayingAttackAnim = false;
        }
    }

    IEnumerator ReloadRoutine()
    {
        hasAttackInterruptReload = false;
        if (!isReloading && CurrentEquippedWeapon.CanReload())
        {
            isReloading = true;
            if (WeaponData != null)
            {
                reloadDuration = WeaponData.reloadDuration;
                startReloadTime = Time.unscaledTime;
                if (WeaponData.clipOutFx != null && AudioManager.Singleton != null)
                    AudioSource.PlayClipAtPoint(WeaponData.clipOutFx, TempTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
                yield return new WaitForSeconds(reloadDuration);
                if (isServer)
                {
                    var equippedWeapon = CurrentEquippedWeapon;
                    equippedWeapon.Reload();
                    equippedWeapons[selectWeaponIndex] = equippedWeapon;
                    equippedWeapons.Dirty(selectWeaponIndex);
                }
                if (WeaponData.clipInFx != null && AudioManager.Singleton != null)
                    AudioSource.PlayClipAtPoint(WeaponData.clipInFx, TempTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
            }
            // If player still attacking, random new attacking action id
            if (isServer && attackingActionId >= 0 && WeaponData != null)
                attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
            yield return new WaitForEndOfFrame();
            isReloading = false;
            if (isLocalPlayer)
            {
                // If weapon is reload one ammo at a time (like as shotgun), automatically reload more bullets
                // When there is no attack interrupt while reload
                if (WeaponData != null && WeaponData.reloadOneAmmoAtATime && CurrentEquippedWeapon.CanReload())
                {
                    if (!hasAttackInterruptReload)
                        Reload();
                    else
                        Attack();
                }
            }
        }
    }

    [Server]
    public void ReceiveDamage(CharacterEntity attacker, int damage)
    {
        if (Hp <= 0 || isInvincible)
            return;

        RpcEffect(attacker.netId, RPC_EFFECT_DAMAGE_HIT);
        int reduceHp = damage;
        if (Armor > 0)
        {
            if (Armor - damage >= 0)
            {
                // Armor absorb damage
                reduceHp -= Mathf.CeilToInt(damage * TotalArmorReduceDamage);
                Armor -= damage;
            }
            else
            {
                // Armor remaining less than 0, Reduce HP by remain damage without armor absorb
                // Armor absorb damage
                reduceHp -= Mathf.CeilToInt(Armor * TotalArmorReduceDamage);
                // Remain damage
                reduceHp -= Mathf.Abs(Armor - damage);
                Armor = 0;
            }
        }
        // Avoid increasing hp by damage
        if (reduceHp < 0)
            reduceHp = 0;

        Hp -= reduceHp;
        if (attacker != null)
        {
            var leechHpAmount = Mathf.CeilToInt(attacker.TotalDamageRateLeechHp * reduceHp);
            attacker.Hp += leechHpAmount;
            if (Hp == 0)
            {
                InterruptAttack();
                InterruptReload();
                RpcInterruptAttack();
                RpcInterruptReload();
                attacker.KilledTarget(this);
            }
        }
    }

    [Server]
    public void KilledTarget(CharacterEntity target)
    {
        var gameplayManager = GameplayManager.Singleton;
        var targetLevel = target.level;
        Exp += Mathf.CeilToInt(gameplayManager.GetRewardExp(targetLevel) * TotalExpRate);
        Score += Mathf.CeilToInt(gameplayManager.GetKillScore(targetLevel) * TotalScoreRate);
        ++killCount;
    }

    [Server]
    public void Heal(int amount)
    {
        if (Hp <= 0)
            return;

        Hp += amount;
    }

    public float GetAttackRange()
    {
        if (WeaponData == null || WeaponData.damagePrefab == null)
            return 0;
        return WeaponData.damagePrefab.GetAttackRange();
    }

    private void OnCharacterChanged(string value)
    {
        selectCharacter = value;
        if (characterModel != null)
            Destroy(characterModel.gameObject);
        characterData = GameInstance.GetCharacter(value);
        if (characterData == null || characterData.modelObject == null)
            return;
        characterModel = Instantiate(characterData.modelObject, characterModelTransform);
        characterModel.transform.localPosition = Vector3.zero;
        characterModel.transform.localEulerAngles = Vector3.zero;
        characterModel.transform.localScale = Vector3.one;
        if (headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        if (WeaponData != null)
            characterModel.SetWeaponModel(WeaponData.rightHandObject, WeaponData.leftHandObject, WeaponData.shieldObject);
        characterModel.gameObject.SetActive(true);
    }

    private void OnHeadChanged(string value)
    {
        selectHead = value;
        headData = GameInstance.GetHead(value);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
    }

    private void OnWeaponChanged(int value)
    {
        selectWeaponIndex = value;
        if (selectWeaponIndex < 0 || selectWeaponIndex >= equippedWeapons.Count)
            return;
        if (characterModel != null && WeaponData != null)
            characterModel.SetWeaponModel(WeaponData.rightHandObject, WeaponData.leftHandObject, WeaponData.shieldObject);
    }

    private void OnWeaponsChanged(string value)
    {
        selectWeapons = value;
        // Changes weapon list, equip first weapon equipped position
        if (isServer)
        {
            var splitedData = selectWeapons.Split('|');
            var minEquipPos = int.MaxValue;
            for (var i = 0; i < splitedData.Length; ++i)
            {
                var singleData = splitedData[i];
                var weaponData = GameInstance.GetWeapon(singleData);

                if (weaponData == null)
                    continue;

                var equipPos = weaponData.equipPosition;
                if (minEquipPos > equipPos)
                {
                    if (defaultWeaponIndex == -1)
                        defaultWeaponIndex = i;
                    minEquipPos = equipPos;
                }

                var equippedWeapon = new EquippedWeapon();
                equippedWeapon.defaultId = weaponData.GetId();
                equippedWeapon.weaponId = weaponData.GetId();
                equippedWeapon.SetMaxAmmo();
                equippedWeapons[equipPos] = equippedWeapon;
                equippedWeapons.Dirty(equipPos);
            }
        }
    }

    protected void InterruptAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            isPlayingAttackAnim = false;
        }
    }

    protected void InterruptReload()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            isReloading = false;
        }
    }

    protected virtual void OnSpawn() { }

    [Server]
    public void ServerInvincible()
    {
        invincibleTime = Time.unscaledTime;
        isInvincible = true;
    }

    [Server]
    public void ServerSpawn(bool isWatchedAds)
    {
        var gameplayManager = GameplayManager.Singleton;
        if (!isWatchedAds || watchAdsCount >= gameplayManager.watchAdsRespawnAvailable)
            Reset();
        else
        {
            ++watchAdsCount;
            isPlayingAttackAnim = false;
            isReloading = false;
            isDead = false;
            Hp = TotalHp;
            for (var i = 0; i < equippedWeapons.Count; ++i)
            {
                var equippedWeapon = equippedWeapons[i];
                equippedWeapon.SetMaxAmmo();
                equippedWeapons[i] = equippedWeapon;
                equippedWeapons.Dirty(i);
            }
            selectWeaponIndex = defaultWeaponIndex;
            OnWeaponChanged(selectWeaponIndex);
            RpcWeaponChanged(selectWeaponIndex);
        }
        ServerInvincible();
        OnSpawn();
        var position = gameplayManager.GetCharacterSpawnPosition();
        TempTransform.position = position;
        if (connectionToClient != null)
            TargetSpawn(connectionToClient, position);
    }

    [Server]
    public void ServerRespawn(bool isWatchedAds)
    {
        var gameplayManager = GameplayManager.Singleton;
        if (Time.unscaledTime - deathTime >= gameplayManager.respawnDuration)
            ServerSpawn(isWatchedAds);
    }

    [Server]
    public void Reset()
    {
        Score = 0;
        Exp = 0;
        level = 1;
        statPoint = 0;
        killCount = 0;
        watchAdsCount = 0;
        addStats = new CharacterStats();
        isPlayingAttackAnim = false;
        isReloading = false;
        isDead = false;
        Hp = TotalHp;
        Armor = 0;

        for (var i = 0; i < equippedWeapons.Count; ++i)
        {
            var equippedWeapon = equippedWeapons[i];
            equippedWeapon.ChangeWeaponId(equippedWeapon.defaultId, 0);
            equippedWeapon.SetMaxAmmo();
            equippedWeapons[i] = equippedWeapon;
            equippedWeapons.Dirty(i);
        }
        selectWeaponIndex = defaultWeaponIndex;
        OnWeaponChanged(selectWeaponIndex);
        RpcWeaponChanged(selectWeaponIndex);
    }

    [Server]
    public void ServerReload()
    {
        if (WeaponData != null)
        {
            // Start reload routine at server to reload ammo
            reloadRoutine = StartCoroutine(ReloadRoutine());
            // Call RpcReload() at clients to play reloading animation
            RpcReload();
        }
    }

    [Server]
    public void ServerChangeWeapon(int index)
    {
        if (index >= 0 && index < MAX_EQUIPPABLE_WEAPON_AMOUNT && !equippedWeapons[index].IsEmpty())
        {
            selectWeaponIndex = index;
            InterruptAttack();
            InterruptReload();
            RpcInterruptAttack();
            RpcInterruptReload();
        }
    }

    [Server]
    public bool ServerChangeEquippedWeapon(WeaponData weaponData, int ammoAmount)
    {
        if (weaponData == null || weaponData.equipPosition < 0 || weaponData.equipPosition >= equippedWeapons.Count)
            return false;
        var equipPosition = weaponData.equipPosition;
        var equippedWeapon = equippedWeapons[equipPosition];
        var updated = equippedWeapon.ChangeWeaponId(weaponData.GetId(), ammoAmount);
        if (updated)
        {
            InterruptAttack();
            InterruptReload();
            RpcInterruptAttack();
            RpcInterruptReload();
            equippedWeapons[equipPosition] = equippedWeapon;
            equippedWeapons.Dirty(equipPosition);
            // Trigger change weapon
            if (selectWeaponIndex == equipPosition)
            {
                selectWeaponIndex = defaultWeaponIndex;
                OnWeaponChanged(selectWeaponIndex);
                RpcWeaponChanged(selectWeaponIndex);
            }
        }
        return updated;
    }

    [Server]
    public bool ServerFillWeaponAmmo(WeaponData weaponData, int ammoAmount)
    {
        if (weaponData == null || weaponData.equipPosition < 0 || weaponData.equipPosition >= equippedWeapons.Count)
            return false;
        var equipPosition = weaponData.equipPosition;
        var equippedWeapon = equippedWeapons[equipPosition];
        var updated = false;
        if (equippedWeapon.weaponId.Equals(weaponData.GetId()))
        {
            updated = equippedWeapon.AddReserveAmmo(ammoAmount);
            if (updated)
            {
                equippedWeapons[equipPosition] = equippedWeapon;
                equippedWeapons.Dirty(equipPosition);
            }
        }
        return updated;
    }

    [Command]
    public void CmdReady()
    {
        if (!isReady)
        {
            ServerSpawn(false);
            GameplayManager.Singleton.UpdateRank(netId);
            isReady = true;
        }
    }

    [Command]
    public void CmdRespawn(bool isWatchedAds)
    {
        ServerRespawn(isWatchedAds);
    }

    [Command]
    public void CmdAttack()
    {
        if (WeaponData != null)
            attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
        else
            attackingActionId = -1;
    }

    [Command]
    public void CmdStopAttack()
    {
        attackingActionId = -1;
    }

    [Command]
    public void CmdReload()
    {
        ServerReload();
    }

    [Command]
    public void CmdAddAttribute(string name)
    {
        if (statPoint > 0)
        {
            var gameplay = GameplayManager.Singleton;
            CharacterAttributes attribute;
            if (gameplay.attributes.TryGetValue(name, out attribute))
            {
                addStats += attribute.stats;
                --statPoint;
            }
        }
    }

    [Command]
    public void CmdChangeWeapon(int index)
    {
        ServerChangeWeapon(index);
    }

    [ClientRpc]
    public void RpcReload()
    {
        if (!isServer)
            reloadRoutine = StartCoroutine(ReloadRoutine());
    }

    [ClientRpc]
    public void RpcInterruptAttack()
    {
        if (!isServer)
            InterruptAttack();
    }

    [ClientRpc]
    public void RpcInterruptReload()
    {
        if (!isServer)
            InterruptReload();
    }

    [ClientRpc]
    private void RpcWeaponChanged(int index)
    {
        if (!isServer)
            OnWeaponChanged(index);
    }
    
    [ClientRpc]
    public void RpcEffect(NetworkInstanceId triggerId, byte effectType)
    {
        GameObject triggerObject = null;
        if (isServer)
            triggerObject = NetworkServer.FindLocalObject(triggerId);
        else
            triggerObject = ClientScene.FindLocalObject(triggerId);

        if (triggerObject != null)
        {
            if (effectType == RPC_EFFECT_DAMAGE_SPAWN || effectType == RPC_EFFECT_DAMAGE_HIT)
            {
                var attacker = triggerObject.GetComponent<CharacterEntity>();
                if (attacker != null &&
                    attacker.WeaponData != null &&
                    attacker.WeaponData.damagePrefab != null)
                {
                    var damagePrefab = attacker.WeaponData.damagePrefab;
                    switch (effectType)
                    {
                        case RPC_EFFECT_DAMAGE_SPAWN:
                            EffectEntity.PlayEffect(damagePrefab.spawnEffectPrefab, effectTransform);
                            break;
                        case RPC_EFFECT_DAMAGE_HIT:
                            EffectEntity.PlayEffect(damagePrefab.hitEffectPrefab, effectTransform);
                            break;
                    }
                }
            }
            else if (effectType == RPC_EFFECT_TRAP_HIT)
            {
                var trap = triggerObject.GetComponent<TrapEntity>();
                if (trap != null)
                    EffectEntity.PlayEffect(trap.hitEffectPrefab, effectTransform);
            }
        }
    }

    [TargetRpc]
    private void TargetDead(NetworkConnection conn)
    {
        deathTime = Time.unscaledTime;
    }

    [TargetRpc]
    private void TargetSpawn(NetworkConnection conn, Vector3 position)
    {
        transform.position = position;
    }

    public int CompareTo(CharacterEntity other)
    {
        return ((-1 * Score.CompareTo(other.Score)) * 10) + netId.Value.CompareTo(other.netId.Value);
    }
}
