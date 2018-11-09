using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatchRewardHandler : MonoBehaviour
{
    public static int lastMatchRank = 0;
    public static AdsReward lastMatchReward;

    public UILastMatchResult uiLastMatchResult;
    
    private void Start()
    {
        if (lastMatchRank > 0)
        {
            if (uiLastMatchResult != null)
                uiLastMatchResult.Show();
            var currencies = lastMatchReward.currencies;
            foreach (var currency in currencies)
            {
                MonetizationManager.Save.AddCurrency(currency.id, currency.amount);
            }
            var items = lastMatchReward.items;
            foreach (var item in items)
            {
                MonetizationManager.Save.AddPurchasedItem(item.name);
            }
            lastMatchRank = 0;
        }
    }

    public static void SetRewards(int rank, MatchReward[] rewards)
    {
        if (rewards == null || rank - 1 < 0 || rank - 1 >= rewards.Length || lastMatchRank > 0)
            return;
        lastMatchReward = rewards[rank - 1].RandomReward();
        lastMatchRank = rank;
    }
}
