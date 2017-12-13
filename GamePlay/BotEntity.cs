using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotEntity : CharacterEntity
{
    public enum Characteristic
    {
        Normal,
        NoneAttack
    }
    public const float ReachedTargetDistance = 0.1f;
    public float updateMovementDuration = 2;
    public float attackDuration = 0;
    public float randomMoveDistance = 5f;
    public float detectEnemyDistance = 2f;
    public float turnSpeed = 5f;
    public bool useCustomMoveSpeed;
    public float customMoveSpeed;
    public Characteristic characteristic;
    public CharacterStats startAddStats;
    private Vector3 targetPosition;
    private float lastUpdateMovementTime;
    private float lastAttackTime;
    private bool isWallHit;
    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerSpawn(false);
        lastUpdateMovementTime = Time.unscaledTime - updateMovementDuration;
        lastAttackTime = Time.unscaledTime - attackDuration;
    }

    public override void OnStartLocalPlayer()
    {
        // Do nothing
    }

    protected override void UpdateMovements()
    {
        if (!isServer)
            return;

        if (GameNetworkManager.Singleton.numPlayers <= 0)
        {
            TempRigidbody.velocity = Vector3.zero;
            return;
        }

        if (Hp <= 0)
        {
            ServerRespawn(false);
            TempRigidbody.velocity = Vector3.zero;
            return;
        }
        // Bots will update target movement when reached move target / hitting the walls / it's time
        var isReachedTarget = IsReachedTargetPosition();
        if (isReachedTarget || isWallHit || Time.unscaledTime - lastUpdateMovementTime >= updateMovementDuration)
        {
            lastUpdateMovementTime = Time.unscaledTime;
            targetPosition = new Vector3(
                TempTransform.position.x + Random.Range(-randomMoveDistance, randomMoveDistance),
                0,
                TempTransform.position.z + Random.Range(-randomMoveDistance, randomMoveDistance));
            isWallHit = false;
        }

        var rotatePosition = targetPosition;
        CharacterEntity enemy;
        if (FindEnemy(out enemy) && !isReloading && characteristic == Characteristic.Normal && Time.unscaledTime - lastAttackTime >= attackDuration)
        {
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
                var nextPosition = CurrentEquippedWeapon.WeaponData.equipPosition + 1;
                if (nextPosition < equippedWeapons.Count && !equippedWeapons[nextPosition].IsEmpty())
                    ServerChangeWeapon(nextPosition);
            }
            rotatePosition = enemy.TempTransform.position;
        }
        else if (attackingActionId >= 0)
            attackingActionId = -1;

        // Gets a vector that points from the player's position to the target's.
        var heading = targetPosition - TempTransform.position;
        var distance = heading.magnitude;
        var direction = heading / distance; // This is now the normalized direction.
        var moveSpeed = useCustomMoveSpeed ? customMoveSpeed : TotalMoveSpeed;
        Vector3 movementDir = direction * moveSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
        TempRigidbody.velocity = movementDir;
        var rotateHeading = rotatePosition - TempTransform.position;
        var targetRotation = Quaternion.LookRotation(rotateHeading);
        TempTransform.rotation = Quaternion.Lerp(TempTransform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    }

    private bool IsReachedTargetPosition()
    {
        return Vector3.Distance(targetPosition, TempTransform.position) < ReachedTargetDistance;
    }

    private bool FindEnemy(out CharacterEntity enemy)
    {
        enemy = null;
        var colliders = Physics.OverlapSphere(TempTransform.position, detectEnemyDistance);
        foreach (var collider in colliders)
        {
            var character = collider.GetComponent<CharacterEntity>();
            if (character != null && character != this && character.Hp > 0)
            {
                enemy = character;
                return true;
            }
        }
        return false;
    }

    private void OnCollisionStay(Collision collision)
    {
        var character = collision.gameObject.GetComponent<CharacterEntity>();
        var damage = collision.gameObject.GetComponent<DamageEntity>();

        if (character == null && damage == null)
            isWallHit = true;
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();
        addStats += startAddStats;
        Hp = TotalHp;
    }
}
