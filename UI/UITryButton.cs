using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UITryButton : MonoBehaviour
{
    public UIInGameProductData uiProductData;
    private UIMainMenu uiMainMenu;

    private void Start()
    {
        uiMainMenu = FindObjectOfType<UIMainMenu>();
    }

    public void OnClickTry()
    {
        var characterModel = uiMainMenu.characterModel;
        var characterData = uiMainMenu.characterData;
        var weaponData = uiMainMenu.weaponData;
        var headData = uiMainMenu.headData;

        if (uiProductData.productData is CharacterData)
        {
            characterData = uiProductData.productData as CharacterData;
            Destroy(characterModel.gameObject);
            if (characterData == null || characterData.modelObject == null)
                return;
            characterModel = Instantiate(characterData.modelObject, uiMainMenu.characterModelTransform);
            characterModel.transform.localPosition = Vector3.zero;
            characterModel.transform.localEulerAngles = Vector3.zero;
            characterModel.transform.localScale = Vector3.one;
            if (headData != null)
                characterModel.SetHeadModel(headData.modelObject);
            if (weaponData != null)
                characterModel.SetWeaponModel(weaponData.rightHandObject, weaponData.leftHandObject, weaponData.shieldObject);
            characterModel.gameObject.SetActive(true);
        }

        if (uiProductData.productData is HeadData)
        {
            headData = uiProductData.productData as HeadData;
            characterModel.SetHeadModel(headData.modelObject);
        }

        uiMainMenu.characterModel = characterModel;
        uiMainMenu.characterData = characterData;
        uiMainMenu.weaponData = weaponData;
        uiMainMenu.headData = headData;
    }
}
