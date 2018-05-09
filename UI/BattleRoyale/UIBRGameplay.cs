using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIBRGameplay : MonoBehaviour
{
    public static UIBRGameplay Singleton { get; private set; }
    public GameObject uiSpawn;
    public GameObject uiRankResult;
    public Text textRank;
    public Text textGameState;
    public Text textAlivePlayers;
    public Text textCurrentTimeCount;
    public Image currentTimeCountGage;
    public float gameStateWarnVisibleDuration = 3f;

    private void Awake()
    {
        Singleton = this;
        if (uiRankResult != null)
            uiRankResult.SetActive(false);
    }

    private void Update()
    {
        var localCharacter = BaseNetworkGameCharacter.Local;
        var networkManager = BaseNetworkGameManager.Singleton;
        var brGameplayManager = GameplayManager.Singleton as BRGameplayManager;
        if (brGameplayManager != null)
        {
            var time = System.TimeSpan.FromSeconds(brGameplayManager.CurrentCountdown);
            var timeDiff = brGameplayManager.currentDuration - brGameplayManager.CurrentCountdown;
            var timeText = time.Minutes.ToString("D2") + ":" + time.Seconds.ToString("D2");
            if (textCurrentTimeCount != null)
                textCurrentTimeCount.text = timeText;

            if (currentTimeCountGage != null)
                currentTimeCountGage.fillAmount = brGameplayManager.currentDuration == 0 ? 1 : (brGameplayManager.currentDuration - brGameplayManager.CurrentCountdown) / brGameplayManager.currentDuration;

            if (textAlivePlayers != null)
                textAlivePlayers.text = brGameplayManager.countAliveCharacters.ToString("N0") + "/" + brGameplayManager.countAllCharacters.ToString("N0");

            if (textGameState != null)
            {
                switch (brGameplayManager.currentState)
                {
                    case BRState.WaitingForPlayers:
                        textGameState.gameObject.SetActive(true);
                        textGameState.text = string.Format("Waiting for players...\n{0}", timeText);
                        break;
                    case BRState.WaitingForFirstCircle:
                        textGameState.gameObject.SetActive(true);
                        textGameState.text = string.Format("Safe zone will appear in\n{0}", timeText);
                        break;
                    case BRState.ShrinkDelaying:
                        textGameState.gameObject.SetActive(timeDiff < gameStateWarnVisibleDuration);
                        textGameState.text = string.Format("Safe zone shrinking in\n{0}", timeText);
                        break;
                    case BRState.Shrinking:
                        textGameState.gameObject.SetActive(timeDiff < gameStateWarnVisibleDuration);
                        textGameState.text = string.Format("Safe zone is shrinking", timeText);
                        break;
                    case BRState.LastCircle:
                        textGameState.gameObject.SetActive(false);
                        break;
                }
            }

            if (localCharacter != null && uiSpawn != null)
            {
                var brCharacter = localCharacter.GetComponent<BRCharacterEntityExtra>();
                if (brCharacter != null && brGameplayManager.currentState != BRState.WaitingForPlayers)
                    uiSpawn.SetActive(!brCharacter.isSpawned);
                else if (brGameplayManager.currentState == BRState.WaitingForPlayers)
                    uiSpawn.SetActive(false);
            }
        }
    }

    public void OnClickSpawn()
    {
        var localCharacter = BaseNetworkGameCharacter.Local;
        if (localCharacter != null)
        {
            var brCharacter = localCharacter.GetComponent<BRCharacterEntityExtra>();
            if (brCharacter != null)
                brCharacter.CmdCharacterSpawn();
        }
    }

    public void ShowRankResult(int rank)
    {
        var networkManager = BaseNetworkGameManager.Singleton;
        var brGameplayManager = GameplayManager.Singleton as BRGameplayManager;
        if (brGameplayManager != null)
        {
            if (textRank != null)
                textRank.text = rank.ToString("N0") + "/" + brGameplayManager.countAllCharacters.ToString("N0");

            if (uiRankResult != null)
                uiRankResult.SetActive(true);
        }
    }
}
