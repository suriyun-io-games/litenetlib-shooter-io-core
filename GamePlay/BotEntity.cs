using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class BotEntity : CharacterEntity
{
    public enum Characteristic
    {
        Aggressive,
        NoneAttack
    }
    public override bool IsBot
    {
        get { return true; }
    }

    public override CharacterStats SumAddStats
    {
        get
        {
            if (refreshingSumAddStats)
            {
                CharacterStats addStats = base.SumAddStats;
                addStats += startAddStats;
                sumAddStats = addStats;
            }
            return sumAddStats;
        }
    }

    public const float ReachedTargetDistance = 0.1f;
    [Header("Bot configs")]
    public float minimumAttackRange = 5f;
    public float updateMovementDuration = 2f;
    public float attackDuration = 0f;
    public float forgetEnemyDuration = 3f;
    public float randomDashDurationMin = 3f;
    public float randomDashDurationMax = 5f;
    [FormerlySerializedAs("randomMoveDistance")]
    public float randomMoveDistanceMin = 5f;
    public float randomMoveDistanceMax = 5f;
    [FormerlySerializedAs("detectEnemyDistance")]
    public float detectEnemyDistanceMin = 2f;
    public float detectEnemyDistanceMax = 2f;
    public float turnSpeed = 5f;
    public int[] navMeshAreas = new int[] { 0, 1, 2 };
    public Characteristic characteristic;
    public CharacterStats startAddStats;
    [HideInInspector, System.NonSerialized]
    public bool isFixRandomMoveAroundPoint;
    [HideInInspector, System.NonSerialized]
    public Vector3 fixRandomMoveAroundPoint;
    [HideInInspector, System.NonSerialized]
    public float fixRandomMoveAroundDistance;
    private float lastUpdateMovementTime;
    private float lastAttackTime;
    private float randomDashDuration;
    private CharacterEntity enemy;
    private Vector3 dashDirection;
    private Queue<Vector3> navPaths = new Queue<Vector3>();
    private Vector3 targetPosition;
    private Vector3 lookingPosition;

    public override void OnStartServer()
    {
        base.OnStartServer();

        ServerSpawn(false);
        lastUpdateMovementTime = Time.unscaledTime - updateMovementDuration;
        lastAttackTime = Time.unscaledTime - attackDuration;
        dashingTime = Time.unscaledTime;
        randomDashDuration = dashDuration + Random.Range(randomDashDurationMin, randomDashDurationMax);
    }

    public override void OnStartOwnerClient()
    {
        // Do nothing
    }

    public int RandomPosNeg()
    {
        return Random.value > 0.5f ? -1 : 1;
    }

    protected override void UpdateMovements()
    {
        if (!IsServer)
            return;

        if (GameNetworkManager.Singleton.PlayersCount <= 0)
        {
            isBlocking = false;
            attackingActionId = -1;
            return;
        }

        if (Hp <= 0)
        {
            ServerRespawn(false);
            return;
        }

        // Bots will update target movement when reached move target / hitting the walls / it's time
        var isReachedTarget = IsReachedTargetPosition();
        if (isReachedTarget || Time.unscaledTime - lastUpdateMovementTime >= updateMovementDuration)
        {
            lastUpdateMovementTime = Time.unscaledTime;
            if (enemy != null)
            {
                GetMovePaths(new Vector3(
                    enemy.CacheTransform.position.x + (Random.Range(detectEnemyDistanceMin, detectEnemyDistanceMax) * RandomPosNeg()),
                    0,
                    enemy.CacheTransform.position.z + (Random.Range(detectEnemyDistanceMin, detectEnemyDistanceMax) * RandomPosNeg())));
            }
            else if (isFixRandomMoveAroundPoint)
            {
                GetMovePaths(new Vector3(
                    fixRandomMoveAroundPoint.x + (fixRandomMoveAroundDistance * RandomPosNeg()),
                    0,
                    fixRandomMoveAroundPoint.z + (fixRandomMoveAroundDistance * RandomPosNeg())));
            }
            else
            {
                GetMovePaths(new Vector3(
                    CacheTransform.position.x + (Random.Range(randomMoveDistanceMin, randomMoveDistanceMax) * RandomPosNeg()),
                    0,
                    CacheTransform.position.z + (Random.Range(randomMoveDistanceMin, randomMoveDistanceMax) * RandomPosNeg())));
            }
        }

        lookingPosition = targetPosition;
        if (enemy == null || enemy.IsDead || Time.unscaledTime - lastAttackTime >= forgetEnemyDuration)
        {
            enemy = null;
            // Try find enemy. If found move to target in next frame
            switch (characteristic)
            {
                case Characteristic.Aggressive:
                    if (FindEnemy(out enemy))
                    {
                        lastAttackTime = Time.unscaledTime;
                        lastUpdateMovementTime = Time.unscaledTime - updateMovementDuration;
                    }
                    break;
            }
        }
        else
        {
            // Set target rotation to enemy position
            lookingPosition = enemy.CacheTransform.position;
        }

        if (enemy != null)
        {
            switch (characteristic)
            {
                case Characteristic.Aggressive:
                    if (Time.unscaledTime - lastAttackTime >= attackDuration &&
                        Vector3.Distance(enemy.CacheTransform.position, CacheTransform.position) < GetAttackRange())
                    {
                        // Attack when nearby enemy
                        if (WeaponData != null)
                        {
                            attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
                            lastAttackTime = Time.unscaledTime;
                            if (CurrentEquippedWeapon.currentReserveAmmo > 0)
                            {
                                if (CurrentEquippedWeapon.currentAmmo == 0)
                                    ServerReload();
                                else if (attackingActionId < 0)
                                    attackingActionId = WeaponData.GetRandomAttackAnimation().actionId;
                            }
                            else
                            {
                                var nextPosition = WeaponData.equipPosition + 1;
                                if (nextPosition < equippedWeapons.Count && !equippedWeapons[nextPosition].IsEmpty())
                                    ServerChangeWeapon(nextPosition);
                            }
                        }
                        else
                        {
                            // Try find next weapon
                            ServerChangeWeapon(selectWeaponIndex + 1);
                        }
                    }
                    break;
            }
        }

        // Dashing
        if (Time.unscaledTime - dashingTime >= randomDashDuration && !isDashing)
        {
            randomDashDuration = dashDuration + Random.Range(randomDashDurationMin, randomDashDurationMax);
            dashDirection = CacheTransform.forward;
            dashDirection.y = 0;
            dashDirection.Normalize();
            isDashing = true;
            dashingTime = Time.unscaledTime;
            CmdDash();
        }

        // Gets a vector that points from the player's position to the target's.
        isReachedTarget = IsReachedTargetPosition();
        Move(isReachedTarget ? Vector3.zero : (isDashing ? dashDirection : (targetPosition - CacheTransform.position).normalized));

        if (isReachedTarget)
        {
            targetPosition = CacheTransform.position + (CacheTransform.forward * ReachedTargetDistance / 2f);
            if (navPaths.Count > 0)
                targetPosition = navPaths.Dequeue();
        }
        // Rotate to target
        var rotateHeading = lookingPosition - CacheTransform.position;
        var targetRotation = Quaternion.LookRotation(rotateHeading);
        CacheTransform.rotation = Quaternion.Lerp(CacheTransform.rotation, Quaternion.Euler(0, targetRotation.eulerAngles.y, 0), Time.deltaTime * turnSpeed);
        UpdateStatPoint();
    }

    protected override void OnIsBlockingUpdated()
    {
        isBlocking = false;
    }

    protected override void OnAttackingActionIdUpdated()
    {
        attackingActionId = -1;
    }

    void OnDrawGizmos()
    {
        if (path != null && path.corners != null && path.corners.Length > 0)
        {
            for (int i = path.corners.Length - 1; i >= 1; --i)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(path.corners[i], path.corners[i - 1]);
            }
        }
    }

    NavMeshPath path;
    private void GetMovePaths(Vector3 position)
    {
        int areaMask = 0;
        if (navMeshAreas.Length == 0)
        {
            areaMask = NavMesh.AllAreas;
        }
        else
        {
            for (int i = 0; i < navMeshAreas.Length; ++i)
            {
                areaMask = areaMask | 1 << navMeshAreas[i];
            }
        }
        NavMeshPath navPath = new NavMeshPath();
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(position, out navHit, 1000f, areaMask) &&
            NavMesh.CalculatePath(CacheTransform.position, navHit.position, areaMask, navPath))
        {
            path = navPath;
            navPaths = new Queue<Vector3>(navPath.corners);
            // Dequeue first path it's not require for future movement
            navPaths.Dequeue();
            // Set movement
            if (navPaths.Count > 0)
                targetPosition = navPaths.Dequeue();
        }
        else
        {
            targetPosition = position;
        }
    }

    private void UpdateStatPoint()
    {
        if (statPoint <= 0)
            return;
        var dict = new Dictionary<CharacterAttributes, int>();
        var list = GameplayManager.Singleton.Attributes.Values.ToList();
        foreach (var entry in list)
        {
            dict.Add(entry, entry.randomWeight);
        }
        CmdAddAttribute(WeightedRandomizer.From(dict).TakeOne().GetHashId());
    }

    private bool IsReachedTargetPosition()
    {
        if (enemy != null)
            return Vector3.Distance(targetPosition, CacheTransform.position) < Mathf.Min(enemy.CacheCharacterMovement.CacheCharacterController.bounds.size.x, enemy.CacheCharacterMovement.CacheCharacterController.bounds.size.z);
        return Vector3.Distance(targetPosition, CacheTransform.position) < ReachedTargetDistance;
    }

    private bool FindEnemy(out CharacterEntity enemy)
    {
        enemy = null;
        var gameplayManager = GameplayManager.Singleton;
        var colliders = Physics.OverlapSphere(CacheTransform.position, detectEnemyDistanceMin);
        foreach (var collider in colliders)
        {
            var character = collider.GetComponent<CharacterEntity>();
            if (character != null && character != this && character.Hp > 0 && gameplayManager.CanReceiveDamage(character, this))
            {
                enemy = character;
                return true;
            }
        }
        return false;
    }

    public override bool ReceiveDamage(CharacterEntity attacker, int damage)
    {
        if (base.ReceiveDamage(attacker, damage))
        {
            switch (characteristic)
            {
                case Characteristic.Aggressive:
                    if (enemy == null)
                        enemy = attacker;
                    else if (Random.value > 0.5f)
                        enemy = attacker;
                    break;
            }
            return true;
        }
        return false;
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        Hp = TotalHp;
    }

    public override float GetAttackRange()
    {
        float range = base.GetAttackRange();
        if (range < minimumAttackRange)
            return minimumAttackRange;
        return range;
    }
}
