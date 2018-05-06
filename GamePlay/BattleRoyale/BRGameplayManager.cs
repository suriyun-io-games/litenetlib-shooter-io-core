using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public struct BRCircle
{
    public float radius;
    public Transform circleCenter;
    public float shrinkDelay;
    public float shrinkDuration;
    [Range(0.01f, 1f)]
    public float hpRateDps;
}

[System.Serializable]
public struct BRPattern
{
    public BRCircle[] circles;
}

public enum BRState : byte
{
    WaitingForPlayers,
    ShrinkDelaying,
    Shrinking,
    LastCircle,
}

public class BRGameplayManager : GameplayManager
{
    public float waitForPlayersDuration;
    public BRPattern[] patterns;
    public GameObject circleObject;
    public float circleRadiusScale = 1f;
    [SyncVar]
    public int currentCircle;
    [SyncVar]
    public float currentRadius;
    [SyncVar]
    public Vector3 currentCenterPosition;
    [SyncVar]
    public BRState currentState;
    [SyncVar]
    public float timeCountdown;

    public float currentCircleHpRateDps { get; private set; }
    private float currentShrinkDuration;
    private float startShrinkRadius;
    private Vector3 startShrinkCenterPosition;
    private BRPattern randomedPattern;
    private bool serverStarted;

    public override void OnStartServer()
    {
        serverStarted = true;
        currentCircle = 0;
        currentRadius = 0;
        currentState = BRState.WaitingForPlayers;
        timeCountdown = waitForPlayersDuration;
        currentCircleHpRateDps = 0;
        randomedPattern = patterns[Random.Range(0, patterns.Length)];
    }

    private void Update()
    {
        UpdateGameplay();
        if (circleObject != null)
        {
            circleObject.SetActive(currentState != BRState.WaitingForPlayers);
            circleObject.transform.localScale = Vector3.one * circleRadiusScale * currentRadius * 2f;
            circleObject.transform.position = currentCenterPosition;
        }
    }

    private void UpdateGameplay()
    {
        if (!serverStarted)
            return;
        
        timeCountdown -= Time.deltaTime;
        BRCircle circle;
            switch (currentState)
        {
            case BRState.WaitingForPlayers:
                // Start game immediately when players are full
                if (timeCountdown <= 0)
                {
                    currentCircle = 0;
                    if (TryGetCircle(out circle))
                    {
                        currentState = BRState.ShrinkDelaying;
                        timeCountdown = circle.shrinkDelay;
                        currentCircleHpRateDps = circle.hpRateDps;
                        startShrinkRadius = currentRadius = circle.radius;
                        startShrinkCenterPosition = currentCenterPosition = circle.circleCenter.position;
                    }
                    else
                    {
                        currentState = BRState.LastCircle;
                        timeCountdown = 0;
                    }
                }
                break;
            case BRState.ShrinkDelaying:
                if (timeCountdown <= 0)
                {
                    if (TryGetCircle(out circle))
                    {
                        currentState = BRState.Shrinking;
                        currentShrinkDuration = timeCountdown = circle.shrinkDuration;
                        currentCircleHpRateDps = circle.hpRateDps;
                        startShrinkRadius = currentRadius = circle.radius;
                        startShrinkCenterPosition = currentCenterPosition = circle.circleCenter.position;
                    }
                    else
                    {
                        currentState = BRState.LastCircle;
                        timeCountdown = 0;
                    }
                }
                break;
            case BRState.Shrinking:
                if (timeCountdown <= 0)
                {
                    ++currentCircle;
                    BRCircle nextCircle;
                    if (TryGetCircle(out circle) && TryGetCircle(currentCircle + 1, out nextCircle))
                    {
                        currentState = BRState.ShrinkDelaying;
                        timeCountdown = circle.shrinkDelay;
                        currentCircleHpRateDps = circle.hpRateDps;
                    }
                    else
                    {
                        currentState = BRState.LastCircle;
                        timeCountdown = 0;
                    }
                }
                break;
            case BRState.LastCircle:
                timeCountdown = 0;
                break;
        }

        if (currentState != BRState.WaitingForPlayers)
            UpdateCircle();
    }

    private void UpdateCircle()
    {
        BRCircle circle;
        if (currentState == BRState.Shrinking && TryGetCircle(currentCircle + 1, out circle))
        {
            var interp = (currentShrinkDuration - timeCountdown) / currentShrinkDuration;
            currentRadius = Mathf.Lerp(startShrinkRadius, circle.radius, interp);
            currentCenterPosition = Vector3.Lerp(startShrinkCenterPosition, circle.circleCenter.position, interp);
        }
    }

    public bool TryGetCircle(out BRCircle circle)
    {
        return TryGetCircle(currentCircle, out circle);
    }

    public bool TryGetCircle(int currentCircle, out BRCircle circle)
    {
        circle = new BRCircle();
        if (currentCircle < 0 || currentCircle >= randomedPattern.circles.Length)
            return false;
        circle = randomedPattern.circles[currentCircle];
        return true;
    }
}
