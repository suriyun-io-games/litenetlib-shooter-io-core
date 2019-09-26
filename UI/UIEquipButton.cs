using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIEquipButton : MonoBehaviour
{
    public UIInGameProductData uiProductData;
    public Button buttonEquip;
    private UIMainMenu uiMainMenu;

    private void Start()
    {
        uiMainMenu = FindObjectOfType<UIMainMenu>();
    }

    private void Update()
    {
        if (buttonEquip != null)
            buttonEquip.interactable = !IsEquipped();
    }

    private bool IsEquipped()
    {
        if (uiProductData.productData is CharacterData)
        {
            for (var i = 0; i < GameInstance.AvailableCharacters.Count; ++i)
            {
                var item = GameInstance.AvailableCharacters[i];
                if (item == uiProductData.productData)
                {
                    return i == uiMainMenu.SelectCharacter;
                }
            }
        }

        if (uiProductData.productData is HeadData)
        {
            for (var i = 0; i < GameInstance.AvailableHeads.Count; ++i)
            {
                var item = GameInstance.AvailableHeads[i];
                if (item == uiProductData.productData)
                {
                    return i == uiMainMenu.SelectHead;
                }
            }
        }
        return false;
    }

    public void OnClickEquip()
    {
        if (uiProductData.productData is CharacterData)
        {
            for (var i = 0; i < GameInstance.AvailableCharacters.Count; ++i)
            {
                var item = GameInstance.AvailableCharacters[i];
                if (item == uiProductData.productData)
                {
                    uiMainMenu.SelectCharacter = i;
                    break;
                }
            }
        }

        if (uiProductData.productData is HeadData)
        {
            for (var i = 0; i < GameInstance.AvailableHeads.Count; ++i)
            {
                var item = GameInstance.AvailableHeads[i];
                if (item == uiProductData.productData)
                {
                    uiMainMenu.SelectHead = i;
                    break;
                }
            }
        }

        uiMainMenu.OnClickSaveData();
    }
}
