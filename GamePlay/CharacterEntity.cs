using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using static LiteNetLibManager.LiteNetLibSyncList;

[RequireComponent(typeof(LiteNetLibTransform))]
[RequireComponent(typeof(CharacterMovement))]
public class CharacterEntity : BaseNetworkGameCharacter
{
    public const float DISCONNECT_WHEN_NOT_RESPAWN_DURATION = 60;
    public const byte RPC_EFFECT_DAMAGE_SPAWN = 0;
    public const byte RPC_EFFECT_DAMAGE_HIT = 1;
    public const byte RPC_EFFECT_TRAP_HIT = 2;
    public const byte RPC_EFFECT_MUZZLE_SPAWN_R = 3;
    public const byte RPC_EFFECT_MUZZLE_SPAWN_L = 4;
    public const int MAX_EQUIPPABLE_WEAPON_AMOUNT = 10;

    public enum ViewMode
    {
        TopDown,
        ThirdPerson,
    }

    [System.Serializable]
    public class ViewModeSettings
    {
        public Vector3 targetOffsets = Vector3.zero;
        public float zoomDistance = 3f;
        public float minZoomDistance = 3f;
        public float maxZoomDistance = 3f;
        public float xRotation = 45f;
        public float minXRotation = 45f;
        public float maxXRotation = 45f;
        public float yRotation = 0f;
        public float fov = 60f;
        public float nearClipPlane = 0.3f;
        public float farClipPlane = 1000f;
    }

    public ViewMode viewMode;
    public ViewModeSettings topDownViewModeSettings;
    public ViewModeSettings thirdPersionViewModeSettings;
    public bool doNotLockCursor;
    public Transform damageLaunchTransform;
    public Transform effectTransform;
    public Transform characterModelTransform;
    public GameObject[] localPlayerObjects;
    public float dashDuration = 1.5f;
    public float dashMoveSpeedMultiplier = 1.5f;
    public float blockMoveSpeedMultiplier = 0.75f;
    public float returnToMoveDirectionDelay = 1f;
    public float endActionDelay = 0.75f;
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
    [SyncField]
    public int hp;
    public int Hp
    {
        get { return hp; }
        set
        {
            if (!IsServer)
                return;

            if (value <= 0)
            {
                value = 0;
                if (!isDead)
                {
                    TargetDead(ConnectionId);
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

    [SyncField]
    public int armor;
    public int Armor
    {
        get { return armor; }
        set
        {
            if (!IsServer)
                return;

            if (value <= 0)
                value = 0;

            if (value > TotalArmor)
                value = TotalArmor;
            armor = value;
        }
    }

    [SyncField]
    public int exp;
    public virtual int Exp
    {
        get { return exp; }
        set
        {
            if (!IsServer)
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

    [SyncField]
    public int level = 1;

    [SyncField]
    public int statPoint;

    [SyncField]
    public int watchAdsCount;

    [SyncField(onChangeMethodName = nameof(OnCharacterChanged))]
    public int selectCharacter = 0;

    [SyncField(onChangeMethodName = nameof(OnHeadChanged))]
    public int selectHead = 0;

    public SyncListInt selectWeapons = new SyncListInt();
    public SyncListInt selectCustomEquipments = new SyncListInt();

    [SyncField(onChangeMethodName = nameof(OnWeaponChanged))]
    public int selectWeaponIndex = -1;

    [SyncField]
    public bool isInvincible;

    [SyncField(onUpdateMethodName = nameof(OnIsBlockingUpdated))]
    public bool isBlocking;

    [SyncField(onUpdateMethodName = nameof(OnAttackingActionIdUpdated)), Tooltip("If this value >= 0 it's means character is attacking, so set it to -1 to stop attacks")]
    public int attackingActionId = -1;

    [SyncField(onChangeMethodName = nameof(OnAttributeAmountsChanged), alwaysSync = true)]
    public AttributeAmounts attributeAmounts = new AttributeAmounts(0);

    [SyncField]
    public string extra;

    [SyncField(syncMode = LiteNetLibSyncField.SyncMode.ClientMulticast)]
    public Vector3 aimPosition;

    [HideInInspector]
    public int rank = 0;

    public override bool IsDead
    {
        get { return hp <= 0; }
    }

    public override bool IsBot
    {
        get { return false; }
    }

    public System.Action onDead;
    public readonly HashSet<PickupEntity> PickableEntities = new HashSet<PickupEntity>();
    public SyncListEquippedWeapon equippedWeapons = new SyncListEquippedWeapon();

    protected ViewMode dirtyViewMode;
    protected Camera targetCamera;
    protected Vector3 cameraForward;
    protected Vector3 cameraRight;
    protected FollowCameraControls followCameraControls;
    protected Coroutine attackRoutine;
    protected Coroutine reloadRoutine;
    protected CharacterModel characterModel;
    protected CharacterData characterData;
    protected HeadData headData;
    protected Dictionary<int, CustomEquipmentData> customEquipmentDict = new Dictionary<int, CustomEquipmentData>();
    protected int defaultWeaponIndex = -1;
    protected bool isMobileInput;
    protected Vector3 inputMove;
    protected Vector3 inputDirection;
    protected bool inputAttack;
    protected bool inputJump;
    protected bool isDashing;
    protected Vector3 dashInputMove;
    protected float dashingTime;
    protected Vector3? previousPosition;
    protected Vector3 currentVelocity;
    protected float lastActionTime;
    protected Coroutine endActionDelayCoroutine;

    public float startReloadTime { get; private set; }
    public float reloadDuration { get; private set; }
    public bool isReady { get; private set; }
    public bool isDead { get; private set; }
    public bool isGrounded { get { return CacheCharacterMovement.IsGrounded; } }
    public bool isPlayingAttackAnim { get; private set; }
    public bool isReloading { get; private set; }
    public bool hasAttackInterruptReload { get; private set; }
    public float deathTime { get; private set; }
    public float invincibleTime { get; private set; }
    public bool currentActionIsForLeftHand { get; protected set; }

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
            isHidding = value;
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
                renderer.enabled = !isHidding;
            var canvases = GetComponentsInChildren<Canvas>();
            foreach (var canvas in canvases)
                canvas.enabled = !isHidding;
            var projectors = GetComponentsInChildren<Projector>();
            foreach (var projector in projectors)
                projector.enabled = !isHidding;
        }
    }

    public Transform CacheTransform { get; private set; }
    public CharacterMovement CacheCharacterMovement { get; private set; }
    public LiteNetLibTransform CacheNetTransform { get; private set; }

    protected bool refreshingSumAddStats = true;
    protected CharacterStats sumAddStats = new CharacterStats();
    public virtual CharacterStats SumAddStats
    {
        get
        {
            if (refreshingSumAddStats)
            {
                var addStats = new CharacterStats();
                if (headData != null)
                    addStats += headData.stats;
                if (characterData != null)
                    addStats += characterData.stats;
                if (WeaponData != null)
                    addStats += WeaponData.stats;
                if (customEquipmentDict != null)
                {
                    foreach (var value in customEquipmentDict.Values)
                    {
                        addStats += value.stats;
                    }
                }
                if (attributeAmounts.Dict != null)
                {
                    foreach (var kv in attributeAmounts.Dict)
                    {
                        CharacterAttributes attribute;
                        if (GameplayManager.Singleton.Attributes.TryGetValue(kv.Key, out attribute))
                            addStats += attribute.stats * kv.Value;
                    }
                }
                sumAddStats = addStats;
                refreshingSumAddStats = false;
            }
            return sumAddStats;
        }
    }

    public int TotalHp
    {
        get
        {
            var total = GameplayManager.Singleton.baseMaxHp + SumAddStats.addMaxHp;
            return total;
        }
    }

    public int TotalArmor
    {
        get
        {
            var total = GameplayManager.Singleton.baseMaxArmor + SumAddStats.addMaxArmor;
            return total;
        }
    }

    public int TotalMoveSpeed
    {
        get
        {
            var total = GameplayManager.Singleton.baseMoveSpeed + SumAddStats.addMoveSpeed;
            return total;
        }
    }

    public float TotalWeaponDamageRate
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

    public float TotalReduceDamageRate
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

    public float TotalBlockReduceDamageRate
    {
        get
        {
            var total = GameplayManager.Singleton.baseBlockReduceDamageRate + SumAddStats.addBlockReduceDamageRate;

            var maxValue = GameplayManager.Singleton.maxBlockReduceDamageRate;
            if (total < maxValue)
                return total;
            else
                return maxValue;
        }
    }

    public float TotalArmorReduceDamage
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

    public float TotalExpRate
    {
        get
        {
            var total = 1 + SumAddStats.addExpRate;
            return total;
        }
    }

    public float TotalScoreRate
    {
        get
        {
            var total = 1 + SumAddStats.addScoreRate;
            return total;
        }
    }

    public float TotalHpRecoveryRate
    {
        get
        {
            var total = 1 + SumAddStats.addHpRecoveryRate;
            return total;
        }
    }

    public float TotalArmorRecoveryRate
    {
        get
        {
            var total = 1 + SumAddStats.addArmorRecoveryRate;
            return total;
        }
    }

    public float TotalDamageRateLeechHp
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
        selectWeapons.onOperation = OnWeaponsChanged;
        selectCustomEquipments.onOperation = OnCustomEquipmentsChanged;
        gameObject.layer = GameInstance.Singleton.characterLayer;
        CacheTransform = transform;
        CacheCharacterMovement = gameObject.GetOrAddComponent<CharacterMovement>();
        CacheNetTransform = GetComponent<LiteNetLibTransform>();
        CacheNetTransform.ownerClientCanSendTransform = true;
        if (damageLaunchTransform == null)
            damageLaunchTransform = CacheTransform;
        if (effectTransform == null)
            effectTransform = CacheTransform;
        if (characterModelTransform == null)
            characterModelTransform = CacheTransform;
        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(false);
        }
        deathTime = Time.unscaledTime;
    }

    public override void OnStartServer()
    {
        attackingActionId = -1;
    }

    public override void OnStartOwnerClient()
    {
        base.OnStartOwnerClient();

        followCameraControls = FindObjectOfType<FollowCameraControls>();
        followCameraControls.target = CacheTransform;
        targetCamera = followCameraControls.CacheCamera;
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

        var targetSimulateSpeed = GetMoveSpeed() * (isDashing ? dashMoveSpeedMultiplier : 1f);
        CacheNetTransform.interpolateMode = LiteNetLibTransform.InterpolateMode.FixedSpeed;
        CacheNetTransform.fixedInterpolateSpeed = targetSimulateSpeed;

        if (Hp <= 0)
        {
            if (!IsServer && IsOwnerClient && Time.unscaledTime - deathTime >= DISCONNECT_WHEN_NOT_RESPAWN_DURATION)
                GameNetworkManager.Singleton.StopHost();

            if (IsServer)
            {
                attackingActionId = -1;
                isBlocking = false;
            }
        }

        if (IsServer && isInvincible && Time.unscaledTime - invincibleTime >= GameplayManager.Singleton.invincibleDuration)
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
        UpdateViewMode();
        UpdateAimPosition();
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
        if (!previousPosition.HasValue)
            previousPosition = CacheTransform.position;
        var currentMove = CacheTransform.position - previousPosition.Value;
        currentVelocity = currentMove / Time.deltaTime;
        previousPosition = CacheTransform.position;

        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

        UpdateMovements();
    }

    protected virtual void UpdateInputDirection_TopDown(bool canAttack)
    {
        if (viewMode != ViewMode.TopDown)
            return;
        doNotLockCursor = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        followCameraControls.updateRotation = false;
        followCameraControls.updateZoom = true;
        if (isMobileInput)
        {
            inputDirection = Vector3.zero;
            inputDirection += InputManager.GetAxis("Mouse Y", false) * cameraForward;
            inputDirection += InputManager.GetAxis("Mouse X", false) * cameraRight;
            if (canAttack)
                inputAttack = inputDirection.magnitude != 0;
        }
        else
        {
            inputDirection = (InputManager.MousePosition() - targetCamera.WorldToScreenPoint(CacheTransform.position)).normalized;
            inputDirection = new Vector3(inputDirection.x, 0, inputDirection.y);
            if (canAttack)
                inputAttack = InputManager.GetButton("Fire1");
        }
    }

    protected virtual void UpdateInputDirection_ThirdPerson(bool canAttack)
    {
        if (viewMode != ViewMode.ThirdPerson)
            return;
        if (isMobileInput || doNotLockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (isMobileInput)
        {
            followCameraControls.updateRotation = InputManager.GetButton("CameraRotate");
            followCameraControls.updateZoom = true;
            inputDirection = Vector3.zero;
            inputDirection += InputManager.GetAxis("Mouse Y", false) * cameraForward;
            inputDirection += InputManager.GetAxis("Mouse X", false) * cameraRight;
            if (canAttack)
                inputAttack = InputManager.GetButton("Fire1");
        }
        else
        {
            followCameraControls.updateRotation = true;
            followCameraControls.updateZoom = true;
            inputDirection = (InputManager.MousePosition() - targetCamera.WorldToScreenPoint(CacheTransform.position)).normalized;
            inputDirection = new Vector3(inputDirection.x, 0, inputDirection.y);
            if (canAttack)
                inputAttack = InputManager.GetButton("Fire1");
        }
        if (inputAttack)
            lastActionTime = Time.unscaledTime;
    }

    protected virtual void UpdateViewMode(bool force = false)
    {
        if (!IsOwnerClient)
            return;

        if (force || dirtyViewMode != viewMode)
        {
            dirtyViewMode = viewMode;
            ViewModeSettings settings = viewMode == ViewMode.ThirdPerson ? thirdPersionViewModeSettings : topDownViewModeSettings;
            followCameraControls.limitXRotation = true;
            followCameraControls.limitYRotation = false;
            followCameraControls.limitZoomDistance = true;
            followCameraControls.targetOffset = settings.targetOffsets;
            followCameraControls.zoomDistance = settings.zoomDistance;
            followCameraControls.minZoomDistance = settings.minZoomDistance;
            followCameraControls.maxZoomDistance = settings.maxZoomDistance;
            followCameraControls.xRotation = settings.xRotation;
            followCameraControls.minXRotation = settings.minXRotation;
            followCameraControls.maxXRotation = settings.maxXRotation;
            followCameraControls.yRotation = settings.yRotation;
            targetCamera.fieldOfView = settings.fov;
            targetCamera.nearClipPlane = settings.nearClipPlane;
            targetCamera.farClipPlane = settings.farClipPlane;
        }
    }

    protected virtual void UpdateAimPosition()
    {
        if (!(IsOwnerClient || (IsServer && ConnectionId <= 0)) || !WeaponData)
            return;

        float attackDist = WeaponData.damagePrefab.GetAttackRange();
        switch (viewMode)
        {
            case ViewMode.TopDown:
                // Update aim position
                currentActionIsForLeftHand = CurrentActionIsForLeftHand();
                Transform launchTransform;
                GetDamageLaunchTransform(currentActionIsForLeftHand, out launchTransform);
                aimPosition = launchTransform.position + (CacheTransform.forward * attackDist);
                break;
            case ViewMode.ThirdPerson:
                float distanceToCharacter = Vector3.Distance(CacheTransform.position, followCameraControls.CacheCameraTransform.position);
                float distanceToTarget = attackDist;
                Vector3 lookAtCharacterPosition = targetCamera.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, distanceToCharacter));
                Vector3 lookAtTargetPosition = targetCamera.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, distanceToTarget));
                aimPosition = lookAtTargetPosition;
                RaycastHit[] hits = Physics.RaycastAll(lookAtCharacterPosition, (lookAtTargetPosition - lookAtCharacterPosition).normalized, attackDist);
                for (int i = 0; i < hits.Length; ++i)
                {
                    if (hits[i].transform.root != transform.root)
                        aimPosition = hits[i].point;
                }
                break;
        }
    }

    protected virtual void UpdateAnimation()
    {
        if (characterModel == null)
            return;
        var animator = characterModel.CacheAnimator;
        if (animator == null)
            return;
        if (Hp <= 0)
        {
            animator.SetBool("IsDead", true);
            animator.SetFloat("JumpSpeed", 0);
            animator.SetFloat("MoveSpeed", 0);
            animator.SetBool("IsGround", true);
            animator.SetBool("IsDash", false);
            animator.SetBool("IsBlock", false);
        }
        else
        {
            var velocity = currentVelocity;
            var xzMagnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
            var ySpeed = velocity.y;
            animator.SetBool("IsDead", false);
            animator.SetFloat("JumpSpeed", ySpeed);
            animator.SetFloat("MoveSpeed", xzMagnitude);
            animator.SetBool("IsGround", Mathf.Abs(ySpeed) < 0.5f);
            animator.SetBool("IsDash", isDashing);
            animator.SetBool("IsBlock", isBlocking);
        }

        if (WeaponData != null)
            animator.SetInteger("WeaponAnimId", WeaponData.weaponAnimId);

        animator.SetBool("IsIdle", !animator.GetBool("IsDead") && !animator.GetBool("DoAction") && animator.GetBool("IsGround"));

        if (attackingActionId >= 0 && !isPlayingAttackAnim)
            StartCoroutine(AttackRoutine(attackingActionId));
    }

    protected virtual void UpdateInput()
    {
        if (!IsOwnerClient)
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
        inputMove = Vector3.zero;
        inputDirection = Vector3.zero;
        inputAttack = false;
        if (canControl)
        {
            cameraForward = followCameraControls.CacheCameraTransform.forward;
            cameraForward.y = 0;
            cameraForward = cameraForward.normalized;
            cameraRight = followCameraControls.CacheCameraTransform.right;
            cameraRight.y = 0;
            cameraRight = cameraRight.normalized;
            inputMove = Vector3.zero;
            if (!IsDead)
            {
                inputMove += cameraForward * InputManager.GetAxis("Vertical", false);
                inputMove += cameraRight * InputManager.GetAxis("Horizontal", false);
            }

            // Bloacking
            isBlocking = !IsDead && !isReloading && !isDashing && attackingActionId < 0 && isGrounded && InputManager.GetButton("Block");

            // Jump
            if (!IsDead && !isBlocking && !inputJump)
                inputJump = InputManager.GetButtonDown("Jump") && isGrounded && !isDashing;

            if (!isBlocking && !isDashing)
            {
                UpdateInputDirection_TopDown(canAttack);
                UpdateInputDirection_ThirdPerson(canAttack);
                if (!IsDead)
                {
                    if (InputManager.GetButtonDown("Reload"))
                        Reload();
                    if (GameplayManager.Singleton.autoReload &&
                        CurrentEquippedWeapon.currentAmmo == 0 &&
                        CurrentEquippedWeapon.CanReload())
                        Reload();
                    isDashing = InputManager.GetButtonDown("Dash") && isGrounded;
                }
                if (isDashing)
                {
                    if (isMobileInput)
                        dashInputMove = inputMove.normalized;
                    else
                        dashInputMove = new Vector3(CacheTransform.forward.x, 0f, CacheTransform.forward.z).normalized;
                    inputAttack = false;
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

    protected virtual bool CurrentActionIsForLeftHand()
    {
        if (attackingActionId >= 0)
        {
            AttackAnimation attackAnimation;
            if (WeaponData.AttackAnimations.TryGetValue(attackingActionId, out attackAnimation))
                return attackAnimation.isAnimationForLeftHandWeapon;
        }
        return false;
    }

    protected virtual void Move(Vector3 direction)
    {
        if (direction.sqrMagnitude > 1)
            direction = direction.normalized;
        direction.y = 0;

        var targetSpeed = GetMoveSpeed() * (isBlocking ? blockMoveSpeedMultiplier : (isDashing ? dashMoveSpeedMultiplier : 1f));
        CacheCharacterMovement.UpdateMovement(Time.deltaTime, targetSpeed, direction, inputJump);
    }

    protected virtual void UpdateMovements()
    {
        if (!IsOwnerClient)
            return;

        var moveDirection = inputMove;
        var dashDirection = dashInputMove;

        Move(isDashing ? dashDirection : moveDirection);
        // Turn character to move direction
        if (inputDirection.magnitude <= 0 && inputMove.magnitude > 0 || viewMode == ViewMode.ThirdPerson)
            inputDirection = inputMove;
        if (characterModel && characterModel.CacheAnimator && (characterModel.CacheAnimator.GetBool("DoAction") || Time.unscaledTime - lastActionTime <= returnToMoveDirectionDelay) && viewMode == ViewMode.ThirdPerson)
            inputDirection = cameraForward;
        if (!IsDead)
            Rotate(isDashing ? dashInputMove : inputDirection);

        if (!IsDead && !isBlocking)
        {
            if (inputAttack && GameplayManager.Singleton.CanAttack(this))
                Attack();
            else
                StopAttack();
        }

        inputJump = false;
    }

    protected void Rotate(Vector3 direction)
    {
        if (direction.sqrMagnitude != 0)
            CacheTransform.rotation = Quaternion.LookRotation(direction);
    }

    public void GetDamageLaunchTransform(bool isLeftHandWeapon, out Transform launchTransform)
    {
        if (characterModel == null || !characterModel.TryGetDamageLaunchTransform(isLeftHandWeapon, out launchTransform))
            launchTransform = damageLaunchTransform;
    }

    protected void Attack()
    {
        if (IsOwnerClient)
        {
            // If attacking while reloading, determines that it is reload interrupting
            if (isReloading && FinishReloadTimeRate > 0.8f)
                hasAttackInterruptReload = true;
        }

        if (isPlayingAttackAnim || isReloading || isBlocking || !CurrentEquippedWeapon.CanShoot())
            return;

        if (attackingActionId < 0 && IsOwnerClient)
            CmdAttack();
    }

    protected void StopAttack()
    {
        if (attackingActionId >= 0 && IsOwnerClient)
            CmdStopAttack();
    }

    protected void Reload()
    {
        if (isPlayingAttackAnim || isReloading || !CurrentEquippedWeapon.CanReload())
            return;
        if (IsOwnerClient)
            CmdReload();
    }

    IEnumerator AttackRoutine(int actionId)
    {
        if (!isPlayingAttackAnim &&
            !isReloading &&
            CurrentEquippedWeapon.CanShoot() &&
            Hp > 0 &&
            characterModel != null &&
            characterModel.CacheAnimator != null)
        {
            isPlayingAttackAnim = true;
            AttackAnimation attackAnimation;
            if (WeaponData != null &&
                WeaponData.AttackAnimations.TryGetValue(actionId, out attackAnimation))
            {
                if (endActionDelayCoroutine != null)
                    StopCoroutine(endActionDelayCoroutine);
                // Play attack animation
                characterModel.CacheAnimator.SetBool("DoAction", true);
                characterModel.CacheAnimator.SetInteger("ActionID", attackAnimation.actionId);
                characterModel.CacheAnimator.Play(0, 1, 0);

                // Wait to launch damage entity
                var speed = attackAnimation.speed;
                var animationDuration = attackAnimation.animationDuration;
                var launchDuration = attackAnimation.launchDuration;
                if (launchDuration > animationDuration)
                    launchDuration = animationDuration;
                yield return new WaitForSeconds(launchDuration / speed);

                // Launch damage entity on server only
                if (IsServer)
                {
                    WeaponData.Launch(this, attackAnimation.isAnimationForLeftHandWeapon, aimPosition);
                    var equippedWeapon = CurrentEquippedWeapon;
                    equippedWeapon.DecreaseAmmo();
                    equippedWeapons[selectWeaponIndex] = equippedWeapon;
                    equippedWeapons.Dirty(selectWeaponIndex);
                }

                // Random play shoot sounds
                if (WeaponData.attackFx != null && WeaponData.attackFx.Length > 0 && AudioManager.Singleton != null)
                    AudioSource.PlayClipAtPoint(WeaponData.attackFx[Random.Range(0, WeaponData.attackFx.Length - 1)], CacheTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);

                // Wait till animation end
                yield return new WaitForSeconds((animationDuration - launchDuration) / speed);
            }
            // If player still attacking, random new attacking action id
            if (IsServer && attackingActionId >= 0 && WeaponData != null)
                attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;

            // Attack animation ended
            endActionDelayCoroutine = StartCoroutine(DelayEndAction(endActionDelay));
            isPlayingAttackAnim = false;
        }
    }

    IEnumerator DelayEndAction(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        characterModel.CacheAnimator.SetBool("DoAction", false);
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
                    AudioSource.PlayClipAtPoint(WeaponData.clipOutFx, CacheTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
                yield return new WaitForSeconds(reloadDuration);
                if (IsServer)
                {
                    var equippedWeapon = CurrentEquippedWeapon;
                    equippedWeapon.Reload();
                    equippedWeapons[selectWeaponIndex] = equippedWeapon;
                    equippedWeapons.Dirty(selectWeaponIndex);
                }
                if (WeaponData.clipInFx != null && AudioManager.Singleton != null)
                    AudioSource.PlayClipAtPoint(WeaponData.clipInFx, CacheTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
            }
            // If player still attacking, random new attacking action id
            if (IsServer && attackingActionId >= 0 && WeaponData != null)
                attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
            yield return new WaitForEndOfFrame();
            isReloading = false;
            if (IsOwnerClient)
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

    public virtual bool ReceiveDamage(CharacterEntity attacker, int damage)
    {
        if (!IsServer)
            return false;

        if (Hp <= 0 || isInvincible)
            return false;

        if (!GameplayManager.Singleton.CanReceiveDamage(this, attacker))
            return false;

        int reduceHp = damage;
        reduceHp -= Mathf.CeilToInt(damage * TotalReduceDamageRate);

        // Armor damage absorbing
        if (Armor > 0)
        {
            if (Armor - damage >= 0)
            {
                // Reduce damage, decrease armor
                reduceHp -= Mathf.CeilToInt(damage * TotalArmorReduceDamage);
                Armor -= damage;
            }
            else
            {
                // Armor remaining less than 0, Reduce HP by remain damage without armor absorb
                reduceHp -= Mathf.CeilToInt(Armor * TotalArmorReduceDamage);
                // Remain damage after armor broke
                reduceHp -= Mathf.Abs(Armor - damage);
                Armor = 0;
            }
        }

        // Blocking
        if (isBlocking)
            reduceHp -= Mathf.CeilToInt(damage * TotalBlockReduceDamageRate);

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
        return true;
    }

    public void KilledTarget(CharacterEntity target)
    {
        if (!IsServer)
            return;

        var gameplayManager = GameplayManager.Singleton;
        var targetLevel = target.level;
        var maxLevel = gameplayManager.maxLevel;
        Exp += Mathf.CeilToInt(target.RewardExp * TotalExpRate);
        score += Mathf.CeilToInt(target.KillScore * TotalScoreRate);
        foreach (var rewardCurrency in gameplayManager.rewardCurrencies)
        {
            var currencyId = rewardCurrency.currencyId;
            var amount = rewardCurrency.amount.Calculate(targetLevel, maxLevel);
            TargetRewardCurrency(ConnectionId, currencyId, amount);
        }
        ++killCount;
        GameNetworkManager.Singleton.SendKillNotify(playerName, target.playerName, WeaponData == null ? string.Empty : WeaponData.GetId());
    }

    public void Heal(int amount)
    {
        if (!IsServer)
            return;

        if (Hp <= 0)
            return;

        Hp += amount;
    }

    public virtual float GetAttackRange()
    {
        if (WeaponData == null || WeaponData.damagePrefab == null)
            return 0;
        return WeaponData.damagePrefab.GetAttackRange();
    }

    protected virtual void OnCharacterChanged(int value)
    {
        refreshingSumAddStats = true;
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
        if (customEquipmentDict != null)
        {
            characterModel.ClearCustomModels();
            foreach (var customEquipmentEntry in customEquipmentDict.Values)
            {
                characterModel.SetCustomModel(customEquipmentEntry.containerIndex, customEquipmentEntry.modelObject);
            }
        }
        characterModel.gameObject.SetActive(true);
        UpdateCharacterModelHiddingState();
    }

    protected virtual void OnHeadChanged(int value)
    {
        refreshingSumAddStats = true;
        headData = GameInstance.GetHead(value);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        UpdateCharacterModelHiddingState();
    }

    protected virtual void OnWeaponChanged(int value)
    {
        refreshingSumAddStats = true;
        if (selectWeaponIndex < 0 || selectWeaponIndex >= equippedWeapons.Count)
            return;
        if (characterModel != null && WeaponData != null)
            characterModel.SetWeaponModel(WeaponData.rightHandObject, WeaponData.leftHandObject, WeaponData.shieldObject);
        UpdateCharacterModelHiddingState();
    }

    protected virtual void OnWeaponsChanged(Operation op, int itemIndex)
    {
        // Changes weapon list, equip first weapon equipped position
        if (IsServer)
        {
            while (equippedWeapons.Count < MAX_EQUIPPABLE_WEAPON_AMOUNT)
                equippedWeapons.Add(EquippedWeapon.Empty);

            var minEquipPos = int.MaxValue;
            for (var i = 0; i < selectWeapons.Count; ++i)
            {
                var weaponData = GameInstance.GetWeapon(selectWeapons[i]);

                if (weaponData == null)
                    continue;

                var equipPos = weaponData.equipPosition;
                if (minEquipPos > equipPos)
                {
                    defaultWeaponIndex = equipPos;
                    minEquipPos = equipPos;
                }
                
                var equippedWeapon = new EquippedWeapon();
                equippedWeapon.defaultId = weaponData.GetHashId();
                equippedWeapon.weaponId = weaponData.GetHashId();
                equippedWeapon.SetMaxAmmo();
                equippedWeapons[equipPos] = equippedWeapon;
            }
            selectWeaponIndex = defaultWeaponIndex;
        }
        else if (IsClient)
        {
            // Try change weapon model
            OnWeaponChanged(selectWeaponIndex);
        }
    }

    protected virtual void OnCustomEquipmentsChanged(Operation op, int itemIndex)
    {
        refreshingSumAddStats = true;
        if (characterModel != null)
            characterModel.ClearCustomModels();
        customEquipmentDict.Clear();
        for (var i = 0; i < selectCustomEquipments.Count; ++i)
        {
            var customEquipmentData = GameInstance.GetCustomEquipment(selectCustomEquipments[i]);
            if (customEquipmentData != null &&
                !customEquipmentDict.ContainsKey(customEquipmentData.containerIndex))
            {
                customEquipmentDict[customEquipmentData.containerIndex] = customEquipmentData;
                if (characterModel != null)
                    characterModel.SetCustomModel(customEquipmentData.containerIndex, customEquipmentData.modelObject);
            }
        }
        UpdateCharacterModelHiddingState();
    }

    protected virtual void OnIsBlockingUpdated()
    {

    }

    protected virtual void OnAttackingActionIdUpdated()
    {

    }

    protected virtual void OnAttributeAmountsChanged(AttributeAmounts value)
    {
        refreshingSumAddStats = true;
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

    public void ServerInvincible()
    {
        if (!IsServer)
            return;
        invincibleTime = Time.unscaledTime;
        isInvincible = true;
    }

    public void ServerSpawn(bool isWatchedAds)
    {
        if (!IsServer)
            return;
        if (Respawn(isWatchedAds))
        {
            ServerInvincible();
            OnSpawn();
            var position = GetSpawnPosition();
            CacheTransform.position = position;
            TargetSpawn(ConnectionId, position);
            ServerRevive();
        }
    }

    public void ServerRespawn(bool isWatchedAds)
    {
        if (!IsServer)
            return;
        if (CanRespawn(isWatchedAds))
            ServerSpawn(isWatchedAds);
    }

    public void ServerRevive()
    {
        if (!IsServer)
            return;
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

    public void ServerReload()
    {
        if (!IsServer)
            return;
        if (WeaponData != null)
        {
            // Start reload routine at server to reload ammo
            reloadRoutine = StartCoroutine(ReloadRoutine());
            // Call RpcReload() at clients to play reloading animation
            RpcReload();
        }
    }

    public void ServerChangeWeapon(int index)
    {
        if (!IsServer)
            return;
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

    public bool ServerChangeSelectWeapon(WeaponData weaponData, int ammoAmount)
    {
        if (!IsServer)
            return false;
        if (weaponData == null || string.IsNullOrEmpty(weaponData.GetId()) || weaponData.equipPosition < 0 || weaponData.equipPosition >= equippedWeapons.Count)
            return false;
        var equipPosition = weaponData.equipPosition;
        var equippedWeapon = equippedWeapons[equipPosition];
        var updated = equippedWeapon.ChangeWeaponId(weaponData.GetHashId(), ammoAmount);
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

    public bool ServerFillWeaponAmmo(WeaponData weaponData, int ammoAmount)
    {
        if (!IsServer)
            return false;
        if (weaponData == null || weaponData.equipPosition < 0 || weaponData.equipPosition >= equippedWeapons.Count)
            return false;
        var equipPosition = weaponData.equipPosition;
        var equippedWeapon = equippedWeapons[equipPosition];
        var updated = false;
        if (equippedWeapon.weaponId == weaponData.GetHashId())
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

    public void CmdReady()
    {
        CallNetFunction(_CmdReady, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdReady()
    {
        if (!isReady)
        {
            ServerSpawn(false);
            isReady = true;
        }
    }

    public void CmdRespawn(bool isWatchedAds)
    {
        CallNetFunction(_CmdRespawn, FunctionReceivers.Server, isWatchedAds);
    }

    [NetFunction]
    protected void _CmdRespawn(bool isWatchedAds)
    {
        ServerRespawn(isWatchedAds);
    }

    public void CmdAttack()
    {
        CallNetFunction(_CmdAttack, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdAttack()
    {
        if (WeaponData != null)
            attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
        else
            attackingActionId = -1;
    }

    public void CmdStopAttack()
    {
        CallNetFunction(_CmdStopAttack, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdStopAttack()
    {
        attackingActionId = -1;
    }

    public void CmdReload()
    {
        CallNetFunction(_CmdReload, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdReload()
    {
        ServerReload();
    }

    public void CmdAddAttribute(int id)
    {
        CallNetFunction(_CmdAddAttribute, FunctionReceivers.Server, id);
    }

    [NetFunction]
    protected void _CmdAddAttribute(int id)
    {
        if (statPoint > 0)
        {
            if (GameplayManager.Singleton.Attributes.ContainsKey(id))
            {
                attributeAmounts = attributeAmounts.Increase(id, 1);
                --statPoint;
            }
        }
    }

    public void CmdChangeWeapon(int index)
    {
        CallNetFunction(_CmdChangeWeapon, FunctionReceivers.Server, index);
    }

    [NetFunction]
    protected void _CmdChangeWeapon(int index)
    {
        ServerChangeWeapon(index);
    }

    public void CmdDash()
    {
        CallNetFunction(_CmdDash, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdDash()
    {
        // Play dash animation on other clients
        RpcDash();
    }

    public void CmdPickup(uint netId)
    {
        CallNetFunction(_CmdPickup, FunctionReceivers.Server, netId);
    }

    [NetFunction]
    protected void _CmdPickup(uint netId)
    {
        LiteNetLibIdentity go;
        if (!Manager.Assets.TryGetSpawnedObject(netId, out go))
            return;
        var pickup = go.GetComponent<PickupEntity>();
        if (pickup == null)
            return;
        pickup.Pickup(this);
    }

    public void RpcReload()
    {
        CallNetFunction(_RpcReload, FunctionReceivers.All);
    }

    [NetFunction]
    protected void _RpcReload()
    {
        if (!IsServer)
            reloadRoutine = StartCoroutine(ReloadRoutine());
    }

    public void RpcInterruptAttack()
    {
        CallNetFunction(_RpcInterruptAttack, FunctionReceivers.All);
    }

    [NetFunction]
    protected void _RpcInterruptAttack()
    {
        if (!IsServer)
            InterruptAttack();
    }

    public void RpcInterruptReload()
    {
        CallNetFunction(_RpcInterruptReload, FunctionReceivers.All);
    }

    [NetFunction]
    protected void _RpcInterruptReload()
    {
        if (!IsServer)
            InterruptReload();
    }

    public void RpcWeaponChanged(int index)
    {
        CallNetFunction(_RpcWeaponChanged, FunctionReceivers.All, index);
    }

    [NetFunction]
    protected void _RpcWeaponChanged(int index)
    {
        if (!IsServer)
            OnWeaponChanged(index);
    }

    public void RpcEffect(uint triggerId, byte effectType)
    {
        CallNetFunction(_RpcEffect, FunctionReceivers.All, triggerId, effectType);
    }

    [NetFunction]
    protected void _RpcEffect(uint triggerId, byte effectType)
    {
        if (IsHidding)
            return;
        LiteNetLibIdentity triggerObject;
        if (Manager.Assets.TryGetSpawnedObject(triggerId, out triggerObject))
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
                        case RPC_EFFECT_MUZZLE_SPAWN_R:
                            Transform muzzleRTransform;
                            GetDamageLaunchTransform(false, out muzzleRTransform);
                            EffectEntity.PlayEffect(damagePrefab.muzzleEffectPrefab, muzzleRTransform);
                            break;
                        case RPC_EFFECT_MUZZLE_SPAWN_L:
                            Transform muzzleLTransform;
                            GetDamageLaunchTransform(true, out muzzleLTransform);
                            EffectEntity.PlayEffect(damagePrefab.muzzleEffectPrefab, muzzleLTransform);
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

    public void RpcDash()
    {
        CallNetFunction(_RpcDash, FunctionReceivers.All);
    }

    [NetFunction]
    protected void _RpcDash()
    {
        // Just play dash animation on another clients
        if (!IsOwnerClient)
        {
            isDashing = true;
            dashingTime = Time.unscaledTime;
        }
    }

    public void TargetDead(long conn)
    {
        CallNetFunction(_TargetDead, conn);
    }

    [NetFunction]
    protected void _TargetDead()
    {
        deathTime = Time.unscaledTime;
    }

    public void TargetSpawn(long conn, Vector3 position)
    {
        CallNetFunction(_TargetSpawn, conn, position);
    }

    [NetFunction]
    protected void _TargetSpawn(Vector3 position)
    {
        transform.position = position;
    }

    public void TargetRewardCurrency(long conn, string currencyId, int amount)
    {
        CallNetFunction(_TargetRewardCurrency, conn, currencyId, amount);
    }

    [NetFunction]
    protected void _TargetRewardCurrency(string currencyId, int amount)
    {
        MonetizationManager.Save.AddCurrency(currencyId, amount);
    }
}
