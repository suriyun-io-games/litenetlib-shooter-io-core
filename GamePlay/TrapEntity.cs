using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(Rigidbody))]
public class TrapEntity : NetworkBehaviour
{
    public const float ReachedTargetDistance = 0.1f;
    public float triggerableDuration = 2;
    public int triggeredDamage = 5;
    public EffectEntity hitEffectPrefab;
    public float moveSpeed = 10f;
    public float turnSpeed = 5f;
    public Transform[] moveWaypoints;
    public readonly List<Transform> MoveWaypoints = new List<Transform>();
    public readonly Dictionary<NetworkInstanceId, float> TriggerredTime = new Dictionary<NetworkInstanceId, float>();
    private Vector3 targetPosition;
    private int currentWaypoint;
    private bool isReversing;

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
        foreach (var moveWaypoint in moveWaypoints)
        {
            if (moveWaypoint == null)
                continue;
            MoveWaypoints.Add(moveWaypoint);
        }
        currentWaypoint = 0;
        if (MoveWaypoints.Count > 0)
            targetPosition = MoveWaypoints[currentWaypoint].position;
    }

    private void Update()
    {
        if (!isServer)
            return;

        if (MoveWaypoints.Count <= 1)
            return;

        if (IsReachedTargetPosition())
        {
            if (!isReversing)
            {
                ++currentWaypoint;
                if (currentWaypoint == MoveWaypoints.Count)
                {
                    currentWaypoint = MoveWaypoints.Count - 1;
                    isReversing = true;
                }
            }
            else
            {
                --currentWaypoint;
                if (currentWaypoint == -1)
                {
                    currentWaypoint = 0;
                    isReversing = false;
                }
            }
            targetPosition = MoveWaypoints[currentWaypoint].position;
        }
        // Gets a vector that points from the player's position to the target's.
        var heading = targetPosition - TempTransform.position;
        var distance = heading.magnitude;
        var direction = heading / distance; // This is now the normalized direction.
        Vector3 movementDir = direction * moveSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
        TempRigidbody.velocity = new Vector3(movementDir.x, TempRigidbody.velocity.y, movementDir.z);
        var targetRotation = Quaternion.LookRotation(heading);
        TempTransform.rotation = Quaternion.Lerp(TempTransform.rotation, Quaternion.Euler(0, targetRotation.eulerAngles.y, 0), Time.deltaTime * turnSpeed);
    }

    private bool IsReachedTargetPosition()
    {
        return Vector3.Distance(targetPosition, TempTransform.position) < ReachedTargetDistance;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isServer)
            return;

        var character = other.GetComponent<CharacterEntity>();
        if (character == null)
            return;

        var characterNetId = character.netId;
        var time = Time.unscaledTime;
        if (TriggerredTime.ContainsKey(characterNetId) && time - TriggerredTime[characterNetId] < triggerableDuration)
            return;

        TriggerredTime[characterNetId] = time;
        character.Hp -= triggeredDamage;
        character.RpcEffect(netId, CharacterEntity.RPC_EFFECT_TRAP_HIT);
    }
}
