using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIFilterUnlockItemList : MonoBehaviour
{
    public UIProductList uiProductList;

    private void Start()
    {
        Filter();
    }

    public void Filter()
    {
        if (uiProductList == null)
            return;

        var unlockUIs = new List<UIProductData>();
        foreach (var ui in uiProductList.GetUIs())
        {
            if (ui.productData is ItemData && (ui.productData as ItemData).IsUnlock())
            {
                unlockUIs.Add(ui);
            }
        }

        for (var i = unlockUIs.Count - 1; i >= 0; --i)
        {
            unlockUIs[i].transform.SetAsFirstSibling();
        }
    }
}
