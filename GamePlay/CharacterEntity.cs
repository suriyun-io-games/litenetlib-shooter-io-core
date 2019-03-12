using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody))]
public class CharacterEntity : BaseNetworkGameCharacter
{
    public const float DISCONNECT_WHEN_NOT_RESPAWN_DURATION = 60;
    public const byte RPC_EFFECT_DAMAGE_SPAWN = 0;
    public const byte RPC_EFFECT_DAMAGE_HIT = 1;
    public const byte RPC_EFFECT_TRAP_HIT = 2;
    public const int MAX_EQUIPPABLE_WEAPON_AMOUNT = 10;
    public Transform damageLaunchTransform;
    public Transform effectTransform;
    public Transform characterModelTransform;
    public GameObject[] localPlayerObjects;
    public float jumpHeight = 2f;
    public float dashDuration = 1.5f;
    public float dashMoveSpeedMultiplier = 1.5f;
    [Header("UI")]
    public Transform hpBarContainer;
    public Image hpFillImage;
    public Text hpText;
    public Image armorFillImage;
    public Text armorText;
    public Text nameText;
    public Text levelText;
    public GameObject attackSignalObject;
    [Header("Effect")]
    public GameObject invincibleEffect;
    [Header("Online data")]

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
                    PickableEntities.Clear();
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
    public virtual int Exp
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

    [SyncVar]
    public string extra;

    [HideInInspector]
    public int rank = 0;

    public override bool IsDead
    {
        get { return hp <= 0; }
    }
    
    public System.Action onDead;
    public readonly HashSet<PickupEntity> PickableEntities = new HashSet<PickupEntity>();
    public SyncListEquippedWeapon equippedWeapons = new SyncListEquippedWeapon();

    protected Coroutine attackRoutine;
    protected Coroutine reloadRoutine;
    protected Camera targetCamera;
    protected CharacterModel characterModel;
    protected CharacterData characterData;
    protected HeadData headData;
    protected int defaultWeaponIndex = -1;
    protected bool isMobileInput;
    protected Vector2 inputMove;
    protected Vector2 inputDirection;
    protected bool inputAttack;
    protected bool inputJump;
    protected bool isDashing;
    protected Vector2 dashInputMove;
    protected float dashingTime;

    public float startReloadTime { get; private set; }
    public float reloadDuration { get; private set; }
    public bool isReady { get; private set; }
    public bool isDead { get; private set; }
    public bool isGround { get; private set; }
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

    private bool isHidding;
    public bool IsHidding
    {
        get { return isHidding; }
        set
        {
            if (isHidding == value)
                return;

            isHidding = value;
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
                renderer.enabled = !isHidding;
            var canvases = GetComponentsInChildren<Canvas>();
            foreach (var canvas in canvases)
                canvas.enabled = !isHidding;
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

    public virtual CharacterStats SumAddStats
    {
        get
        {
            var stats = new CharacterStats();
            stats += addStats;
            if (headData != null)
                stats += headData.stats;
            if (characterData != null)
                stats += characterData.stats;
            if (WeaponData != null)
                stats += WeaponData.stats;
            return stats;
        }
    }

    public virtual int TotalHp
    {
        get
        {
            var total = GameplayManager.Singleton.baseMaxHp + SumAddStats.addMaxHp;
            return total;
        }
    }

    public virtual int TotalArmor
    {
        get
        {
            var total = GameplayManager.Singleton.baseMaxArmor + SumAddStats.addMaxArmor;
            return total;
        }
    }
    
    public virtual int TotalMoveSpeed
    {
        get
        {
            var total = GameplayManager.Singleton.baseMoveSpeed + SumAddStats.addMoveSpeed;
            return total;
        }
    }

    public virtual float TotalWeaponDamageRate
    {
        get
        {
            var total = GameplayManager.Singleton.baseWeaponDamageRate + SumAddStats.addWeaponDamageRate;

            var maxValue = GameplayManager.Singleton.maxWeaponDamageRate;
            if (total < maxValue)
                return total;
            else
                return maxValue;
        }
    }

    public virtual float TotalReduceDamageRate
    {
        get
        {
            var total = GameplayManager.Singleton.baseReduceDamageRate + SumAddStats.addReduceDamageRate;

            var maxValue = GameplayManager.Singleton.maxReduceDamageRate;
            if (total < maxValue)
                return total;
            else
                return maxValue;
        }
    }

    public virtual float TotalArmorReduceDamage
    {
        get
        {
            var total = GameplayManager.Singleton.baseArmorReduceDamage + SumAddStats.addArmorReduceDamage;

            var maxValue = GameplayManager.Singleton.maxArmorReduceDamage;
            if (total < maxValue)
                return total;
            else
                return maxValue;
        }
    }

    public virtual float TotalExpRate
    {
        get
        {
            var total = 1 + SumAddStats.addExpRate;
            return total;
        }
    }

    public virtual float TotalScoreRate
    {
        get
        {
            var total = 1 + SumAddStats.addScoreRate;
            return total;
        }
    }

    public virtual float TotalHpRecoveryRate
    {
        get
        {
            var total = 1 + SumAddStats.addHpRecoveryRate;
            return total;
        }
    }

    public virtual float TotalArmorRecoveryRate
    {
        get
        {
            var total = 1 + SumAddStats.addArmorRecoveryRate;
            return total;
        }
    }

    public virtual float TotalDamageRateLeechHp
    {
        get
        {
            var total = SumAddStats.addDamageRateLeechHp;
            return total;
        }
    }

    public virtual int RewardExp
    {
        get { return GameplayManager.Singleton.GetRewardExp(level); }
    }

    public virtual int KillScore
    {
        get { return GameplayManager.Singleton.GetKillScore(level); }
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
        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(false);
        }
        deathTime = Time.unscaledTime;
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
        base.OnStartLocalPlayer();

        var followCam = FindObjectOfType<FollowCamera>();
        followCam.target = TempTransform;
        targetCamera = followCam.GetComponent<Camera>();
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.FadeOut();

        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(true);
        }

        CmdReady();
    }

    protected override void Update()
    {
        base.Update();
        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

        if (Hp <= 0)
        {
            if (!isServer && isLocalPlayer && Time.unscaledTime - deathTime >= DISCONNECT_WHEN_NOT_RESPAWN_DURATION)
                GameNetworkManager.Singleton.StopHost();

            if (isServer)
                attackingActionId = -1;
        }

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
        if (levelText != null)
            levelText.text = level.ToString("N0");
        UpdateAnimation();
        UpdateInput();
        // Update dash state
        if (isDashing && Time.unscaledTime - dashingTime > dashDuration)
            isDashing = false;
        // Update attack signal
        if (attackSignalObject != null)
            attackSignalObject.SetActive(isPlayingAttackAnim);
    }

    private void FixedUpdate()
    {
        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

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
            animator.SetBool("IsDash", false);
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
            animator.SetBool("IsDash", isDashing);
        }

        if (WeaponData != null)
            animator.SetInteger("WeaponAnimId", WeaponData.weaponAnimId);

        animator.SetBool("IsIdle", !animator.GetBool("IsDead") && !animator.GetBool("DoAction") && animator.GetBool("IsGround"));

        if (attackingActionId >= 0 && !isPlayingAttackAnim)
            StartCoroutine(AttackRoutine(attackingActionId));
    }

    protected virtual void UpdateInput()
    {
        if (!isLocalPlayer || Hp <= 0)
            return;

        bool canControl = true;
        var fields = FindObjectsOfType<InputField>();
        foreach (var field in fields)
        {
            if (field.isFocused)
            {
                canControl = false;
                break;
            }
        }

        isMobileInput = Application.isMobilePlatform;
#if UNITY_EDITOR
        isMobileInput = GameInstance.Singleton.showJoystickInEditor;
#endif
        InputManager.useMobileInputOnNonMobile = isMobileInput;

        var canAttack = isMobileInput || !EventSystem.current.IsPointerOverGameObject();
        inputMove = Vector2.zero;
        inputDirection = Vector2.zero;
        inputAttack = false;
        if (canControl)
        {
            inputMove = new Vector2(InputManager.GetAxis("Horizontal", false), InputManager.GetAxis("Vertical", false));

            // Jump
            if (!inputJump)
                inputJump = InputManager.GetButtonDown("Jump") && isGround && !isDashing;

            // Attack, Can attack while not dashing
            if (!isDashing)
            {
                if (isMobileInput)
                {
                    inputDirection = new Vector2(InputManager.GetAxis("Mouse X", false), InputManager.GetAxis("Mouse Y", false));
                    if (canAttack)
                        inputAttack = inputDirection.magnitude != 0;
                }
                else
                {
                    inputDirection = (InputManager.MousePosition() - targetCamera.WorldToScreenPoint(TempTransform.position)).normalized;
                    if (canAttack)
                        inputAttack = InputManager.GetButton("Fire1");
                }
                if (InputManager.GetButtonDown("Reload"))
                    Reload();
                if (GameplayManager.Singleton.autoReload &&
                    CurrentEquippedWeapon.currentAmmo == 0 &&
                    CurrentEquippedWeapon.CanReload())
                    Reload();
            }

            // Dash
            if (!isDashing)
            {
                isDashing = InputManager.GetButtonDown("Dash") && isGround;
                if (isDashing)
                {
                    inputAttack = false;
                    dashInputMove = new Vector2(TempTransform.forward.x, TempTransform.forward.z).normalized;
                    dashingTime = Time.unscaledTime;
                    CmdDash();
                }
            }
        }
    }

    protected virtual float GetMoveSpeed()
    {
        return TotalMoveSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }

    protected virtual void Move(Vector3 direction)
    {
        if (direction.magnitude != 0)
        {
            if (direction.magnitude > 1)
                direction = direction.normalized;

            var targetSpeed = GetMoveSpeed() * (isDashing ? dashMoveSpeedMultiplier : 1f);
            var targetVelocity = direction * targetSpeed;

            // Apply a force that attempts to reach our target velocity
            Vector3 velocity = TempRigidbody.velocity;
            Vector3 velocityChange = (targetVelocity - velocity);
            velocityChange.x = Mathf.Clamp(velocityChange.x, -targetSpeed, targetSpeed);
            velocityChange.y = 0;
            velocityChange.z = Mathf.Clamp(velocityChange.z, -targetSpeed, targetSpeed);
            TempRigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
        }
    }
    
    protected virtual void UpdateMovements()
    {
        if (!isLocalPlayer || Hp <= 0)
            return;

        var moveDirection = new Vector3(inputMove.x, 0, inputMove.y);
        var dashDirection = new Vector3(dashInputMove.x, 0, dashInputMove.y);

        Move(isDashing ? dashDirection : moveDirection);
        Rotate(isDashing ? dashInputMove : inputDirection);

        if (inputAttack && GameplayManager.Singleton.CanAttack(this))
            Attack();
        else
            StopAttack();

        var velocity = TempRigidbody.velocity;
        if (isGround && inputJump)
        {
            TempRigidbody.velocity = new Vector3(velocity.x, CalculateJumpVerticalSpeed(), velocity.z);
            isGround = false;
            inputJump = false;
        }
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (!isGround && collision.impulse.y > 0)
            isGround = true;
    }

    protected virtual void OnCollisionStay(Collision collision)
    {
        if (!isGround && collision.impulse.y > 0)
            isGround = true;
    }

    protected float CalculateJumpVerticalSpeed()
    {
        // From the jump height and gravity we deduce the upwards speed 
        // for the character to reach at the apex.
        return Mathf.Sqrt(2f * jumpHeight * -Physics.gravity.y);
    }

    protected void Rotate(Vector2 direction)
    {
        if (direction.magnitude != 0)
        {
            int newRotation = (int)(Quaternion.LookRotation(new Vector3(direction.x, 0, direction.y)).eulerAngles.y + targetCamera.transform.eulerAngles.y);
            Quaternion targetRotation = Quaternion.Euler(0, newRotation, 0);
            TempTransform.rotation = targetRotation;
        }
    }
    
    public void GetDamageLaunchTransform(bool isLeftHandWeapon, out Transform launchTransform)
    {
        launchTransform = null;
        if (characterModel == null || !characterModel.TryGetDamageLaunchTransform(isLeftHandWeapon, out launchTransform))
            launchTransform = damageLaunchTransform;
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
        if (!isPlayingAttackAnim && 
            !isReloading && 
            CurrentEquippedWeapon.CanShoot() && 
            Hp > 0 &&
            characterModel != null &&
            characterModel.TempAnimator != null)
        {
            isPlayingAttackAnim = true;
            var animator = characterModel.TempAnimator;
            AttackAnimation attackAnimation;
            if (WeaponData != null &&
                WeaponData.AttackAnimations.TryGetValue(actionId, out attackAnimation))
            {
                // Play attack animation
                animator.SetBool("DoAction", false);
                yield return new WaitForEndOfFrame();
                animator.SetBool("DoAction", true);
                animator.SetInteger("ActionID", attackAnimation.actionId);

                // Wait to launch damage entity
                var speed = attackAnimation.speed;
                var animationDuration = attackAnimation.animationDuration;
                var launchDuration = attackAnimation.launchDuration;
                if (launchDuration > animationDuration)
                    launchDuration = animationDuration;
                yield return new WaitForSeconds(launchDuration / speed);

                // Launch damage entity on server only
                if (isServer)
                {
                    WeaponData.Launch(this, attackAnimation.isAnimationForLeftHandWeapon);
                    var equippedWeapon = CurrentEquippedWeapon;
                    equippedWeapon.DecreaseAmmo();
                    equippedWeapons[selectWeaponIndex] = equippedWeapon;
                    equippedWeapons.Dirty(selectWeaponIndex);
                }

                // Random play shoot sounds
                if (WeaponData.attackFx != null && WeaponData.attackFx.Length > 0 && AudioManager.Singleton != null)
                    AudioSource.PlayClipAtPoint(WeaponData.attackFx[Random.Range(0, WeaponData.attackFx.Length - 1)], TempTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);

                // Wait till animation end
                yield return new WaitForSeconds((animationDuration - launchDuration) / speed);
            }
            // If player still attacking, random new attacking action id
            if (isServer && attackingActionId >= 0 && WeaponData != null)
                attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
            yield return new WaitForEndOfFrame();

            // Attack animation ended
            animator.SetBool("DoAction", false);
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
        var gameplayManager = GameplayManager.Singleton;
        if (Hp <= 0 || isInvincible)
            return;

        RpcEffect(attacker.netId, RPC_EFFECT_DAMAGE_HIT);
        if (!gameplayManager.CanReceiveDamage(this))
            return;

        int reduceHp = damage;
        reduceHp -= Mathf.CeilToInt(damage * TotalReduceDamageRate);
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
                // Remain damage after armor broke
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
                if (onDead != null)
                    onDead.Invoke();
                InterruptAttack();
                InterruptReload();
                RpcInterruptAttack();
                RpcInterruptReload();
                attacker.KilledTarget(this);
                ++dieCount;
            }
        }
    }

    [Server]
    public void KilledTarget(CharacterEntity target)
    {
        var gameplayManager = GameplayManager.Singleton;
        var targetLevel = target.level;
        var maxLevel = gameplayManager.maxLevel;
        Exp += Mathf.CeilToInt(target.RewardExp * TotalExpRate);
        score += Mathf.CeilToInt(target.KillScore * TotalScoreRate);
        if (connectionToClient != null)
        {
            foreach (var rewardCurrency in gameplayManager.rewardCurrencies)
            {
                var currencyId = rewardCurrency.currencyId;
                var amount = rewardCurrency.amount.Calculate(targetLevel, maxLevel);
                TargetRewardCurrency(connectionToClient, currencyId, amount);
            }
        }
        ++killCount;
        GameNetworkManager.Singleton.SendKillNotify(playerName, target.playerName, WeaponData == null ? string.Empty : WeaponData.GetId());
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

    protected virtual void OnCharacterChanged(string value)
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
        UpdateCharacterModelHiddingState();
    }

    protected virtual void OnHeadChanged(string value)
    {
        selectHead = value;
        headData = GameInstance.GetHead(value);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        UpdateCharacterModelHiddingState();
    }

    protected virtual void OnWeaponChanged(int value)
    {
        selectWeaponIndex = value;
        if (selectWeaponIndex < 0 || selectWeaponIndex >= equippedWeapons.Count)
            return;
        if (characterModel != null && WeaponData != null)
            characterModel.SetWeaponModel(WeaponData.rightHandObject, WeaponData.leftHandObject, WeaponData.shieldObject);
        UpdateCharacterModelHiddingState();
    }

    protected virtual void OnWeaponsChanged(string value)
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
                    defaultWeaponIndex = equipPos;
                    minEquipPos = equipPos;
                }

                var equippedWeapon = new EquippedWeapon();
                equippedWeapon.defaultId = weaponData.GetId();
                equippedWeapon.weaponId = weaponData.GetId();
                equippedWeapon.SetMaxAmmo();
                equippedWeapons[equipPos] = equippedWeapon;
                equippedWeapons.Dirty(equipPos);
            }
            selectWeaponIndex = defaultWeaponIndex;
        }
    }

    public void UpdateCharacterModelHiddingState()
    {
        if (characterModel == null)
            return;
        var renderers = characterModel.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
            renderer.enabled = !IsHidding;
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

    public virtual Vector3 GetSpawnPosition()
    {
        return GameplayManager.Singleton.GetCharacterSpawnPosition();
    }

    public virtual void OnSpawn() { }

    [Server]
    public void ServerInvincible()
    {
        invincibleTime = Time.unscaledTime;
        isInvincible = true;
    }

    [Server]
    public void ServerSpawn(bool isWatchedAds)
    {
        if (Respawn(isWatchedAds))
        {
            var gameplayManager = GameplayManager.Singleton;
            ServerInvincible();
            OnSpawn();
            var position = GetSpawnPosition();
            TempTransform.position = position;
            if (connectionToClient != null)
                TargetSpawn(connectionToClient, position);
            ServerRevive();
        }
    }

    [Server]
    public void ServerRespawn(bool isWatchedAds)
    {
        if (CanRespawn(isWatchedAds))
            ServerSpawn(isWatchedAds);
    }

    [Server]
    public void ServerRevive()
    {
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

        isPlayingAttackAnim = false;
        isReloading = false;
        isDead = false;
        Hp = TotalHp;
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
        var gameInstance = GameInstance.Singleton;
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
    public bool ServerChangeSelectWeapon(WeaponData weaponData, int ammoAmount)
    {
        if (weaponData == null || string.IsNullOrEmpty(weaponData.GetId()) || weaponData.equipPosition < 0 || weaponData.equipPosition >= equippedWeapons.Count)
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
        if (!string.IsNullOrEmpty(equippedWeapon.weaponId) && equippedWeapon.weaponId.Equals(weaponData.GetId()))
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

    [Command]
    public void CmdDash()
    {
        // Play dash animation on other clients
        RpcDash();
    }

    [Command]
    public void CmdPickup(NetworkInstanceId netId)
    {
        var go = NetworkServer.FindLocalObject(netId);
        if (go == null)
            return;
        var pickup = go.GetComponent<PickupEntity>();
        if (pickup == null)
            return;
        pickup.Pickup(this);
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
        GameObject triggerObject = isServer ? NetworkServer.FindLocalObject(triggerId) : ClientScene.FindLocalObject(triggerId);
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

    [ClientRpc]
    public void RpcDash()
    {
        // Just play dash animation on another clients
        if (!isLocalPlayer)
        {
            isDashing = true;
            dashingTime = Time.unscaledTime;
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

    [TargetRpc]
    private void TargetRewardCurrency(NetworkConnection conn, string currencyId, int amount)
    {
        MonetizationManager.Save.AddCurrency(currencyId, amount);
    }
}
