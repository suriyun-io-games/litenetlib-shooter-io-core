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

    private void Update()
    {
        if (isServer)
        {
            var brGameManager = BRGameplayManager.Singleton;
            if (brGameManager.currentState != BRState.WaitingForPlayers)
            {
                var currentPosition = TempTransform.position;
                currentPosition.y = 0;

                var centerPosition = brGameManager.currentCenterPosition;
                centerPosition.y = 0;

                if (Vector3.Distance(currentPosition, centerPosition) > brGameManager.currentRadius)
                {
                    TempCharacterEntity.Hp -= Mathf.CeilToInt(brGameManager.currentCircleHpRateDps * TempCharacterEntity.Hp);
                }
            }
        }
    }
}
