using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(CharacterEntity))]
public class BRCharacterEntityExtra : NetworkBehaviour
{
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

    private CharacterEntity tempCharacterEntity;
    public CharacterEntity TempCharacterEntity
    {
        get
        {
            if (tempCharacterEntity == null)
                tempCharacterEntity = GetComponent<CharacterEntity>();
            return tempCharacterEntity;
        }
    }

    private float lastCircleCheckTime;

    private void Update()
    {
        if (isServer)
        {
            var brGameManager = GameplayManager.Singleton as BRGameplayManager;
            if (brGameManager.currentState != BRState.WaitingForPlayers && Time.realtimeSinceStartup - lastCircleCheckTime >= 1f)
            {
                var currentPosition = TempTransform.position;
                currentPosition.y = 0;

                var centerPosition = brGameManager.currentCenterPosition;
                centerPosition.y = 0;
                var distance = Vector3.Distance(currentPosition, centerPosition);
                var currentRadius = brGameManager.currentRadius;
                Debug.LogError(brGameManager.currentCircleHpRateDps);
                if (distance > currentRadius)
                    TempCharacterEntity.Hp -= Mathf.CeilToInt(brGameManager.currentCircleHpRateDps * TempCharacterEntity.TotalHp);
                lastCircleCheckTime = Time.realtimeSinceStartup;
            }
        }
    }
}
