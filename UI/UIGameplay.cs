using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class UIGameplay : MonoBehaviour
{
    public Text textLevel;
    public Text textExp;
    public Image fillExp;
    public Text textStatPoint;
    public Text textHp;
    public Text textArmor;
    public Text textRespawnCountDown;
    public Text textWatchedAdsCount;
    public UIBlackFade blackFade;
    public GameObject respawnUiContainer;
    public GameObject respawnButtonContainer;
    public GameObject reloadTimeContainer;
    public Image fillReloadTime;
    public Text textReloadTimePercent;
    public UIRandomAttributes randomAttributes;
    public UIEquippedWeapon[] equippedWeapons;
    public UINetworkGameScoreEntry[] userRankings;
    public UINetworkGameScoreEntry localRanking;
    public GameObject[] mobileOnlyUis;
    public GameObject[] hidingIfDedicateServerUis;
    private bool isNetworkActiveDirty;
    private bool isRespawnShown;
    private bool isRandomedAttributesShown;
    private bool canRandomAttributes;

    private void Awake()
    {
        foreach (var mobileOnlyUi in mobileOnlyUis)
        {
            mobileOnlyUi.SetActive(Application.isMobilePlatform);
        }
    }

    private void OnEnable()
    {
        StartCoroutine(SetupCanRandomAttributes());
    }

    IEnumerator SetupCanRandomAttributes()
    {
        canRandomAttributes = false;
        yield return new WaitForSeconds(1);
        canRandomAttributes = true;
    }

    private void Update()
    {
        var isNetworkActive = NetworkManager.singleton.isNetworkActive;
        if (isNetworkActiveDirty != isNetworkActive)
        {
            foreach (var hidingIfDedicateUi in hidingIfDedicateServerUis)
            {
                hidingIfDedicateUi.SetActive(!NetworkServer.active || NetworkServer.localClientActive);
            }
            if (isNetworkActive)
                FadeOut();
            isNetworkActiveDirty = isNetworkActive;
        }

        var localCharacter = BaseNetworkGameCharacter.Local as CharacterEntity;
        if (localCharacter == null)
            return;

        var level = localCharacter.level;
        var exp = localCharacter.Exp;
        var nextExp = GameplayManager.Singleton.GetExp(level);
        if (textLevel != null)
            textLevel.text = "LV" + level.ToString("N0");

        if (textExp != null)
            textExp.text = exp.ToString("N0") + "/" + nextExp.ToString("N0");

        if (fillExp != null)
            fillExp.fillAmount = (float)exp / (float)nextExp;

        if (textStatPoint != null)
            textStatPoint.text = localCharacter.statPoint.ToString("N0");

        if (textHp != null)
            textHp.text = localCharacter.TotalHp.ToString("N0");

        if (textArmor != null)
            textArmor.text = localCharacter.TotalArmor.ToString("N0");

        if (localCharacter.Hp <= 0)
        {
            if (!isRespawnShown)
            {
                if (respawnUiContainer != null)
                    respawnUiContainer.SetActive(true);
                isRespawnShown = true;
            }
            if (isRespawnShown)
            {
                var remainTime = GameplayManager.Singleton.respawnDuration - (Time.unscaledTime - localCharacter.deathTime);
                var watchAdsRespawnAvailable = GameplayManager.Singleton.watchAdsRespawnAvailable;
                if (remainTime < 0)
                    remainTime = 0;
                if (textRespawnCountDown != null)
                    textRespawnCountDown.text = Mathf.Abs(remainTime).ToString("N0");
                if (textWatchedAdsCount != null)
                    textWatchedAdsCount.text = (watchAdsRespawnAvailable - localCharacter.watchAdsCount) + "/" + watchAdsRespawnAvailable;
                if (respawnButtonContainer != null)
                    respawnButtonContainer.SetActive(remainTime == 0);
            }
        }
        else
        {
            if (respawnUiContainer != null)
                respawnUiContainer.SetActive(false);
            isRespawnShown = false;
        }

        if (localCharacter.Hp > 0 && localCharacter.statPoint > 0 && canRandomAttributes)
        {
            if (!isRandomedAttributesShown)
            {
                if (randomAttributes != null)
                {
                    randomAttributes.uiGameplay = this;
                    randomAttributes.gameObject.SetActive(true);
                    randomAttributes.Random();
                }
                isRandomedAttributesShown = true;
            }
        }
        else
        {
            if (randomAttributes != null)
                randomAttributes.gameObject.SetActive(false);
            isRandomedAttributesShown = false;
        }

        for (var i = 0; i < equippedWeapons.Length; ++i)
        {
            var equippedWeapon = equippedWeapons[i];
            if (equippedWeapon == null)
                continue;
            equippedWeapon.indexInAvailableList = i;
            if (i < localCharacter.equippedWeapons.Count && !localCharacter.equippedWeapons[i].IsEmpty())
            {
                equippedWeapon.equippedWeapon = localCharacter.equippedWeapons[i];
                equippedWeapon.gameObject.SetActive(true);
            }
            else
                equippedWeapon.gameObject.SetActive(false);
        }

        if (localCharacter.isReloading)
        {
            if (reloadTimeContainer != null)
                reloadTimeContainer.SetActive(true);
            if (fillReloadTime != null)
                fillReloadTime.fillAmount = localCharacter.FinishReloadTimeRate;
            if (textReloadTimePercent != null)
                textReloadTimePercent.text = (localCharacter.FinishReloadTimeRate * 100).ToString("N0") + "%";
        }
        else
        {
            if (reloadTimeContainer != null)
                reloadTimeContainer.SetActive(false);
        }
    }

    public void UpdateRankings(NetworkGameScore[] rankings)
    {
        for (var i = 0; i < userRankings.Length; ++i)
        {
            var userRanking = userRankings[i];
            if (i < rankings.Length)
            {
                var ranking = rankings[i];
                userRanking.SetData(i + 1, ranking);

                var isLocal = BaseNetworkGameCharacter.Local != null && ranking.netId.Equals(BaseNetworkGameCharacter.Local.netId);
                if (isLocal)
                    UpdateLocalRank(i + 1, ranking);
            }
            else
                userRanking.Clear();
        }
    }

    public void UpdateLocalRank(int rank, NetworkGameScore ranking)
    {
        if (localRanking != null)
            localRanking.SetData(rank, ranking);
    }

    public void AddAttribute(string name)
    {
        var character = BaseNetworkGameCharacter.Local as CharacterEntity;
        if (character == null || character.statPoint == 0)
            return;
        character.CmdAddAttribute(name);
        StartCoroutine(SetupCanRandomAttributes());
    }

    public void Respawn()
    {
        var character = BaseNetworkGameCharacter.Local as CharacterEntity;
        if (character == null)
            return;
        character.CmdRespawn(false);
    }

    public void WatchAdsRespawn()
    {
        var character = BaseNetworkGameCharacter.Local as CharacterEntity;
        if (character == null)
            return;

        if (character.watchAdsCount >= GameplayManager.Singleton.watchAdsRespawnAvailable)
        {
            character.CmdRespawn(false);
            return;
        }
        MonetizationManager.ShowAd(GameInstance.Singleton.watchAdsRespawnPlacement, OnWatchAdsRespawnResult);
    }

    private void OnWatchAdsRespawnResult(MonetizationManager.RemakeShowResult result)
    {
        if (result == MonetizationManager.RemakeShowResult.Finished)
        {
            var character = BaseNetworkGameCharacter.Local as CharacterEntity;
            character.CmdRespawn(true);
        }
    }

    public void ExitGame()
    {
        GameNetworkManager.Singleton.StopHost();
    }

    public void FadeIn()
    {
        if (blackFade != null)
            blackFade.FadeIn();
    }

    public void FadeOut()
    {
        if (blackFade != null)
            blackFade.FadeOut();
    }
}
