using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UILastMatchResult : UIBase
{
    public UIInGameCurrency uiCurrencyPrefab;
    public UIProductData uiProductPrefab;
    public Transform container;
    public Text textRank;

    public override void Show()
    {
        base.Show();
        var childCount = container.childCount;
        for (var i = childCount - 1; i >= 0; --i)
        {
            Destroy(container.GetChild(i).gameObject);
        }

        if (textRank != null)
            textRank.text = MatchRewardHandler.lastMatchRank.ToString("N0");

        foreach (var currency in MatchRewardHandler.lastMatchReward.currencies)
        {
            var newUI = Instantiate(uiCurrencyPrefab, container);
            newUI.transform.localScale = Vector3.one;
            newUI.currency = currency;
        }

        foreach (var item in MatchRewardHandler.lastMatchReward.items)
        {
            var newUI = Instantiate(uiProductPrefab, container);
            newUI.transform.localScale = Vector3.one;
            newUI.productData = item;
        }
    }
}
