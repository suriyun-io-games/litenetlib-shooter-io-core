using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIWeaponSelectEntry : MonoBehaviour
{
    [Header("UI")]
    public Text textTitle;
    public Text textDescription;
    public Text textPrice;
    public RawImage iconImage;
    public RawImage previewImage;
    public Button selectWeaponButton;
    public Text textSelectWeaponButton;
    public string messageWeaponSelected = "Selected";
    public string messageWeaponSelectable = "Select";
    [Header("Weapon Data")]
    public WeaponData weaponData;
    private WeaponData dirtyWeaponData;

    [HideInInspector]
    public int indexInAvailableList;
    [HideInInspector]
    public UIWeaponSelectList list;

    public WeaponData WeaponData
    {
        get { return weaponData as WeaponData; }
    }

    protected virtual void Update()
    {
        if (weaponData != dirtyWeaponData)
        {
            UpdateData();
            dirtyWeaponData = weaponData;
        }
    }

    public void UpdateData()
    {
        var title = "";
        var description = "";
        var price = "";
        Texture iconTexture = null;
        Texture previewTexture = null;
        if (weaponData != null)
        {
            title = weaponData.GetTitle();
            description = weaponData.GetDescription();
            price = weaponData.GetPriceText();
            iconTexture = weaponData.iconTexture;
            previewTexture = weaponData.previewTexture;
        }
        if (textTitle != null)
            textTitle.text = title;
        if (textDescription != null)
            textDescription.text = description;
        if (textPrice != null)
            textPrice.text = price;
        if (iconImage != null)
            iconImage.texture = iconTexture;
        if (previewImage != null)
            previewImage.texture = previewTexture;
    }

    public void UpdateSelectButtonInteractable()
    {
        if (selectWeaponButton == null || weaponData == null)
            return;
        selectWeaponButton.interactable = true;
        if (textSelectWeaponButton != null)
            textSelectWeaponButton.text = messageWeaponSelectable;
        var savedWeapons = PlayerSave.GetWeapons();
        foreach (var savedWeapon in savedWeapons)
        {
            if (GameInstance.GetAvailableWeapon(savedWeapon.Value).GetId().Equals(weaponData.GetId()))
            {
                selectWeaponButton.interactable = false;
                if (textSelectWeaponButton != null)
                    textSelectWeaponButton.text = messageWeaponSelected;
                break;
            }
        }
    }

    public virtual void OnClickSelectWeapon()
    {
        var savedWeapons = PlayerSave.GetWeapons();
        savedWeapons[WeaponData.equipPosition] = indexInAvailableList;
        PlayerSave.SetWeapons(savedWeapons);
        list.UpdateSelectButtonsInteractable();
    }
}
