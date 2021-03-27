using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
public class CharacterMovement : MonoBehaviour
{
    public enum ApplyJumpForceMode
    {
        ApplyImmediately,
        ApplyAfterFixedDuration,
    }

    public const int WATER_LAYER = 4;

    [Header("Movement AI")]
    [Range(0.01f, 1f)]
    public float stoppingDistance = 0.1f;

    [Header("Movement Settings")]
    public float jumpHeight = 2f;
    public ApplyJumpForceMode applyJumpForceMode = ApplyJumpForceMode.ApplyImmediately;
    public float applyJumpForceFixedDuration;
    public float backwardMoveSpeedRate = 0.75f;
    public float gravity = 9.81f;
    public float maxFallVelocity = 40f;
    public float stickGroundForce = 9.6f;
    [Tooltip("Delay before character change from grounded state to airborne")]
    public float airborneDelay = 0.01f;
    public bool doNotChangeVelocityWhileAirborne;
    public float landedPauseMovementDuration = 0f;
    [Range(0.1f, 1f)]
    public float underWaterThreshold = 0.75f;
    public bool autoSwimToSurface;

    public CharacterEntity CacheEntity { get; private set; }
    public Transform CacheTransform { get; private set; }
    public CharacterController CacheCharacterController { get; private set; }

    public bool IsGrounded
    {
        get { return CacheCharacterController.isGrounded; }
    }

    public float StoppingDistance
    {
        get { return stoppingDistance; }
    }

    public Vector3 CurrentVelocity
    {
        get; private set;
    }

    public Queue<Vector3> navPaths { get; private set; }
    public bool HasNavPaths
    {
        get { return navPaths != null && navPaths.Count > 0; }
    }

    // Movement codes
    private float airborneElapsed;
    private bool isUnderWater;
    private bool applyingJumpForce;
    private float applyJumpForceCountDown;
    private Collider waterCollider;
    private Vector3 platformMotion;
    private Transform groundedTransform;
    private Vector3 groundedLocalPosition;
    private Vector3 oldGroundedPosition;
    private Vector3? clientTargetPosition;
    private Vector3 tempMoveDirection;
    private Vector3 tempHorizontalMoveDirection;
    private Vector3 tempMoveVelocity;
    private Vector3 tempTargetPosition;
    private Vector3 tempCurrentPosition;
    private Vector3 tempPredictPosition;
    private Vector3 velocityBeforeAirborne;
    private float tempVerticalVelocity;
    private float tempSqrMagnitude;
    private float tempPredictSqrMagnitude;
    private float tempTargetDistance;
    private float tempEntityMoveSpeed;
    private float tempCurrentMoveSpeed;
    private CollisionFlags collisionFlags;
    private float pauseMovementCountDown;

    protected virtual void Awake()
    {
        CacheTransform = transform;
        CacheEntity = gameObject.GetComponent<CharacterEntity>();
        CacheCharacterController = gameObject.GetOrAddComponent<CharacterController>();
        CurrentVelocity = Vector3.zero;
        StopMoveFunction();
    }

    protected virtual void Start()
    {
        tempCurrentPosition = CacheTransform.position;
        tempVerticalVelocity = 0;
    }

    protected virtual void OnEnable()
    {
        CacheCharacterController.enabled = true;
        tempVerticalVelocity = 0;
    }

    protected virtual void OnDisable()
    {
        CacheCharacterController.enabled = false;
        CurrentVelocity = Vector3.zero;
    }

    public void StopMove()
    {
        StopMoveFunction();
    }

    private void StopMoveFunction()
    {
        navPaths = null;
    }

    public void PointClickMovement(Vector3 position)
    {
        SetMovePaths(position, true);
    }

    private void WaterCheck()
    {
        if (waterCollider == null)
        {
            // Not in water
            isUnderWater = false;
            return;
        }
        float footToSurfaceDist = waterCollider.bounds.max.y - CacheCharacterController.bounds.min.y;
        float currentThreshold = footToSurfaceDist / (CacheCharacterController.bounds.max.y - CacheCharacterController.bounds.min.y);
        isUnderWater = currentThreshold >= underWaterThreshold;
    }

    protected virtual void Update()
    {

    }

    public void UpdateMovement(float deltaTime, float moveSpeed, Vector3 tempInputDirection, bool isJumping)
    {
        if (!enabled)
            return;

        tempCurrentPosition = CacheTransform.position;
        tempMoveVelocity = Vector3.zero;
        tempMoveDirection = Vector3.zero;
        tempTargetDistance = -1f;
        WaterCheck();

        bool isGrounded = CacheCharacterController.isGrounded;
        bool isAirborne = !isGrounded && !isUnderWater && airborneElapsed >= airborneDelay;

        // Update airborne elasped
        if (isGrounded)
            airborneElapsed = 0f;
        else
            airborneElapsed += deltaTime;

        if (HasNavPaths)
        {
            // Set `tempTargetPosition` and `tempCurrentPosition`
            tempTargetPosition = navPaths.Peek();
            tempMoveDirection = (tempTargetPosition - tempCurrentPosition).normalized;
            tempTargetDistance = Vector3.Distance(tempTargetPosition.GetXZ(), tempCurrentPosition.GetXZ());
            if (tempTargetDistance < StoppingDistance)
            {
                navPaths.Dequeue();
                if (!HasNavPaths)
                {
                    StopMove();
                    tempMoveDirection = Vector3.zero;
                }
            }
        }
        else if (clientTargetPosition.HasValue)
        {
            tempTargetPosition = clientTargetPosition.Value;
            tempMoveDirection = (tempTargetPosition - tempCurrentPosition).normalized;
            tempTargetDistance = Vector3.Distance(tempTargetPosition.GetXZ(), tempCurrentPosition.GetXZ());
            if (tempTargetDistance < StoppingDistance)
            {
                clientTargetPosition = null;
                StopMove();
                tempMoveDirection = Vector3.zero;
            }
        }
        else if (tempInputDirection.sqrMagnitude > 0f)
        {
            tempMoveDirection = tempInputDirection.normalized;
            tempTargetPosition = tempCurrentPosition + tempMoveDirection;
        }

        if (CacheEntity.isDead)
        {
            tempMoveDirection = Vector3.zero;
            isJumping = false;
            applyingJumpForce = false;
        }

        // Prepare movement speed
        tempEntityMoveSpeed = applyingJumpForce ? 0f : moveSpeed;
        tempCurrentMoveSpeed = tempEntityMoveSpeed;

        // Calculate vertical velocity by gravity
        if (!isGrounded && !isUnderWater)
        {
            tempVerticalVelocity = Mathf.MoveTowards(tempVerticalVelocity, -maxFallVelocity, gravity * deltaTime);
        }
        else
        {
            // Not falling set verical velocity to 0
            tempVerticalVelocity = 0f;
        }

        // Jumping 
        if (isGrounded && isJumping)
        {
            airborneElapsed = airborneDelay;
            applyingJumpForce = true;
            applyJumpForceCountDown = 0f;
            switch (applyJumpForceMode)
            {
                case ApplyJumpForceMode.ApplyAfterFixedDuration:
                    applyJumpForceCountDown = applyJumpForceFixedDuration;
                    break;
            }
        }

        if (applyingJumpForce)
        {
            applyJumpForceCountDown -= Time.deltaTime;
            if (applyJumpForceCountDown <= 0f)
            {
                isGrounded = false;
                applyingJumpForce = false;
                tempVerticalVelocity = CalculateJumpVerticalSpeed();
            }
        }
        // Updating horizontal movement (WASD inputs)
        if (!isAirborne)
        {
            velocityBeforeAirborne = Vector3.zero;
        }
        if (pauseMovementCountDown <= 0f && tempMoveDirection.sqrMagnitude > 0f && (!isAirborne || !doNotChangeVelocityWhileAirborne))
        {
            // Calculate only horizontal move direction
            tempHorizontalMoveDirection = tempMoveDirection;
            tempHorizontalMoveDirection.y = 0;
            tempHorizontalMoveDirection.Normalize();

            // If character move backward
            if (Vector3.Angle(tempHorizontalMoveDirection, CacheTransform.forward) > 120)
                tempCurrentMoveSpeed *= backwardMoveSpeedRate;

            // NOTE: `tempTargetPosition` and `tempCurrentPosition` were set above
            tempSqrMagnitude = (tempTargetPosition - tempCurrentPosition).sqrMagnitude;
            tempPredictPosition = tempCurrentPosition + (tempHorizontalMoveDirection * tempCurrentMoveSpeed * deltaTime);
            tempPredictSqrMagnitude = (tempPredictPosition - tempCurrentPosition).sqrMagnitude;
            // Check `tempSqrMagnitude` against the `tempPredictSqrMagnitude`
            // if `tempPredictSqrMagnitude` is greater than `tempSqrMagnitude`,
            // rigidbody will reaching target and character is moving pass it,
            // so adjust move speed by distance and time (with physic formula: v=s/t)
            if (tempPredictSqrMagnitude >= tempSqrMagnitude)
                tempCurrentMoveSpeed *= tempTargetDistance / deltaTime / tempCurrentMoveSpeed;
            tempMoveVelocity = tempHorizontalMoveDirection * tempCurrentMoveSpeed;
            velocityBeforeAirborne = tempMoveVelocity;
        }
        // Moves by velocity before airborne
        if (isAirborne)
        {
            if (doNotChangeVelocityWhileAirborne)
                tempMoveVelocity = velocityBeforeAirborne;
            pauseMovementCountDown = landedPauseMovementDuration;
        }
        else
        {
            if (pauseMovementCountDown > 0f)
                pauseMovementCountDown -= deltaTime;
        }
        // Updating vertical movement (Fall, WASD inputs under water)
        if (isUnderWater)
        {
            tempCurrentMoveSpeed = tempEntityMoveSpeed;
            // Move up to surface while under water
            if (autoSwimToSurface || Mathf.Abs(tempMoveDirection.y) > 0)
            {
                if (autoSwimToSurface)
                    tempMoveDirection.y = 1f;
                tempTargetPosition = Vector3.up * (waterCollider.bounds.max.y - (CacheCharacterController.bounds.size.y * underWaterThreshold));
                tempCurrentPosition = Vector3.up * CacheTransform.position.y;
                tempTargetDistance = Vector3.Distance(tempTargetPosition, tempCurrentPosition);
                tempSqrMagnitude = (tempTargetPosition - tempCurrentPosition).sqrMagnitude;
                tempPredictPosition = tempCurrentPosition + (Vector3.up * tempMoveDirection.y * tempCurrentMoveSpeed * deltaTime);
                tempPredictSqrMagnitude = (tempPredictPosition - tempCurrentPosition).sqrMagnitude;
                // Check `tempSqrMagnitude` against the `tempPredictSqrMagnitude`
                // if `tempPredictSqrMagnitude` is greater than `tempSqrMagnitude`,
                // rigidbody will reaching target and character is moving pass it,
                // so adjust move speed by distance and time (with physic formula: v=s/t)
                if (tempPredictSqrMagnitude >= tempSqrMagnitude)
                    tempCurrentMoveSpeed *= tempTargetDistance / deltaTime / tempCurrentMoveSpeed;
                // Swim up to surface
                tempMoveVelocity.y = tempMoveDirection.y * tempCurrentMoveSpeed;
            }
        }
        else
        {
            // Update velocity while not under water
            tempMoveVelocity.y = tempVerticalVelocity;
        }

        platformMotion = Vector3.zero;
        if (isGrounded && !isUnderWater)
        {
            // Apply platform motion
            if (groundedTransform != null && deltaTime > 0.0f)
            {
                Vector3 newGroundedPosition = groundedTransform.TransformPoint(groundedLocalPosition);
                platformMotion = (newGroundedPosition - oldGroundedPosition) / deltaTime;
                oldGroundedPosition = newGroundedPosition;
            }
        }

        Vector3 stickGroundMove = isGrounded && !isUnderWater ? Vector3.down * stickGroundForce * Time.deltaTime : Vector3.zero;
        collisionFlags = CacheCharacterController.Move((tempMoveVelocity + platformMotion) * deltaTime + stickGroundMove);
        if ((collisionFlags & CollisionFlags.CollidedBelow) == CollisionFlags.CollidedBelow ||
            (collisionFlags & CollisionFlags.CollidedAbove) == CollisionFlags.CollidedAbove)
        {
            // Hit something below or above, falling in next frame
            tempVerticalVelocity = 0f;
        }

        CurrentVelocity = tempMoveVelocity;
    }

    private void SetMovePaths(Vector3 position, bool useNavMesh)
    {
        if (useNavMesh)
        {
            NavMeshPath navPath = new NavMeshPath();
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(position, out navHit, 5f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(CacheTransform.position, navHit.position, NavMesh.AllAreas, navPath))
            {
                navPaths = new Queue<Vector3>(navPath.corners);
                // Dequeue first path it's not require for future movement
                navPaths.Dequeue();
            }
        }
        else
        {
            // If not use nav mesh, just move to position by direction
            navPaths = new Queue<Vector3>();
            navPaths.Enqueue(position);
        }
    }

    private float CalculateJumpVerticalSpeed()
    {
        // From the jump height and gravity we deduce the upwards speed 
        // for the character to reach at the apex.
        return Mathf.Sqrt(2f * jumpHeight * gravity);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == WATER_LAYER)
        {
            // Enter water
            waterCollider = other;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == WATER_LAYER)
        {
            // Exit water
            waterCollider = null;
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (CacheCharacterController.isGrounded)
        {
            RaycastHit raycastHit;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.8f, 0.8f, Vector3.down, out raycastHit, 0.1f, -1, QueryTriggerInteraction.Ignore))
            {
                groundedTransform = raycastHit.collider.transform;
                oldGroundedPosition = raycastHit.point;
                groundedLocalPosition = groundedTransform.InverseTransformPoint(oldGroundedPosition);
            }
        }
    }
}
