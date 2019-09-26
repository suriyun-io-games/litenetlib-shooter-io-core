using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIItemUnlockActivatator : MonoBehaviour
{
    public UIInGameProductData uiProductData;
    public GameObject[] activatingWhileUnlockObjects;
    public GameObject[] activatingWhileNotUnlockObjects;

    private void Update()
    {
        if (uiProductData == null || uiProductData.productData == null)
            return;

        var inGameProductData = uiProductData.productData as ItemData;
        foreach (var obj in activatingWhileUnlockObjects)
        {
            obj.SetActive(inGameProductData.IsUnlock());
        }
        foreach (var obj in activatingWhileNotUnlockObjects)
        {
            obj.SetActive(!inGameProductData.IsUnlock());
        }
    }
}
