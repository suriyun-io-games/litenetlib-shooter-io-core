using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIBRMinimap : MonoBehaviour
{
    public RectTransform container;
    public RawImage uiMap;
    public RectTransform uiPlayer;
    public RectTransform uiCurrentCircle;
    public RectTransform uiNextCircle;
    public bool lockCenterAtPlayer;
    public Vector2 uiMapSize;

    private void Awake()
    {
        if (uiMapSize.sqrMagnitude <= 0)
            uiMapSize = container.sizeDelta;
        container.anchorMin = container.anchorMax = Vector3.one * 0.5f;
        container.pivot = Vector2.one;
        container.sizeDelta = uiMapSize;
        container.localPosition = uiMapSize / 2f;
    }

    private void Update()
    {
        var brGameManager = GameplayManager.Singleton as BRGameplayManager;
        var miniMapCamera = BRMiniMapCamera.Singleton;
        if (miniMapCamera == null)
            return;
        var tempCamera = miniMapCamera.TempCamera;

        Vector2 centerOffset = Vector2.zero;
        var localCharacter = BaseNetworkGameCharacter.Local;
        if (localCharacter != null)
        {
            var characterPosition = localCharacter.transform.position;
            var characterRotationY = localCharacter.transform.eulerAngles.y;
            var viewportPoint = tempCamera.WorldToViewportPoint(characterPosition);
            if (lockCenterAtPlayer)
                centerOffset = new Vector2((0.5f - viewportPoint.x) * uiMapSize.x, (0.5f - viewportPoint.y) * uiMapSize.y);
            if (uiPlayer != null)
            {
                uiPlayer.localPosition = new Vector2(viewportPoint.x * uiMapSize.x, viewportPoint.y * uiMapSize.y) - uiMapSize + centerOffset;
                uiPlayer.eulerAngles = Vector3.forward * -characterRotationY;
            }
        }
        if (uiMap != null)
        {
            uiMap.texture = miniMapCamera.mapTexture;
            uiMap.rectTransform.anchorMin = uiMap.rectTransform.anchorMax = Vector3.one * 0.5f;
            uiMap.rectTransform.sizeDelta = uiMapSize;
            uiMap.rectTransform.localPosition = new Vector2(0.5f * uiMapSize.x, 0.5f * uiMapSize.y) - uiMapSize + centerOffset;
        }
        if (brGameManager != null)
        {
            if (uiCurrentCircle != null)
            {
                uiCurrentCircle.gameObject.SetActive(brGameManager.currentState != BRState.WaitingForPlayers && brGameManager.currentState != BRState.WaitingForFirstCircle);
                var currentCenterPosition = brGameManager.currentCenterPosition;
                var viewportPoint = tempCamera.WorldToViewportPoint(currentCenterPosition);
                var radiusViewportPoint = tempCamera.WorldToViewportPoint(currentCenterPosition + (Vector3.one * brGameManager.currentRadius));
                if (brGameManager.currentState != BRState.WaitingForPlayers)
                {
                    uiCurrentCircle.anchorMin = uiCurrentCircle.anchorMax = Vector3.one * 0.5f;
                    uiCurrentCircle.sizeDelta = Mathf.Abs((radiusViewportPoint - viewportPoint).x) * uiMapSize * 2f;
                    uiCurrentCircle.localPosition = new Vector2(viewportPoint.x * uiMapSize.x, viewportPoint.y * uiMapSize.y) - uiMapSize + centerOffset;
                }
            }
            if (uiNextCircle != null)
            {
                uiNextCircle.gameObject.SetActive(brGameManager.currentState != BRState.WaitingForPlayers && brGameManager.currentState != BRState.WaitingForFirstCircle);
                var nextCenterPosition = brGameManager.nextCenterPosition;
                var viewportPoint = tempCamera.WorldToViewportPoint(nextCenterPosition);
                var radiusViewportPoint = tempCamera.WorldToViewportPoint(nextCenterPosition + (Vector3.one * brGameManager.nextRadius));
                if (brGameManager.currentState != BRState.WaitingForPlayers)
                {
                    uiNextCircle.anchorMin = uiNextCircle.anchorMax = Vector3.one * 0.5f;
                    uiNextCircle.sizeDelta = Mathf.Abs((radiusViewportPoint - viewportPoint).x) * uiMapSize * 2f;
                    uiNextCircle.localPosition = new Vector2(viewportPoint.x * uiMapSize.x, viewportPoint.y * uiMapSize.y) - uiMapSize + centerOffset;
                }
            }
        }
    }
}
