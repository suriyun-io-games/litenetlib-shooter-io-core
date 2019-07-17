using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UICustomEquipmentEntry : MonoBehaviour
{
    [Header("UI")]
    public Text textTitle;
    public Text textDescription;
    public Text textPrice;
    public RawImage iconImage;
    public RawImage previewImage;
    public Button selectButton;
    public Text textSelectButton;
    public string messageSelected = "Selected";
    public string messageSelectable = "Select";
    [Header("Weapon Data")]
    public CustomEquipmentData customEquipmentData;
    private CustomEquipmentData dirtyCustomEquipmentData;

    [HideInInspector]
    public int indexInAvailableList;
    [HideInInspector]
    public UICustomEquipmentList list;

    public CustomEquipmentData CustomEquipmentData
    {
        get { return customEquipmentData as CustomEquipmentData; }
    }

    protected virtual void Update()
    {
        if (customEquipmentData != dirtyCustomEquipmentData)
        {
            UpdateData();
            dirtyCustomEquipmentData = customEquipmentData;
        }
    }

    public void UpdateData()
    {
        var title = "";
        var description = "";
        var price = "";
        Texture iconTexture = null;
        Texture previewTexture = null;
        if (customEquipmentData != null)
        {
            title = customEquipmentData.GetTitle();
            description = customEquipmentData.GetDescription();
            price = customEquipmentData.GetPriceText();
            iconTexture = customEquipmentData.iconTexture;
            previewTexture = customEquipmentData.previewTexture;
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
        if (selectButton == null || customEquipmentData == null)
            return;
        selectButton.interactable = true;
        if (textSelectButton != null)
            textSelectButton.text = messageSelectable;
        var savedCustomEquipments = PlayerSave.GetCustomEquipments();
        foreach (var savedCustomEquipment in savedCustomEquipments)
        {
            if (GameInstance.GetAvailableCustomEquipment(savedCustomEquipment.Value).GetId().Equals(customEquipmentData.GetId()))
            {
                selectButton.interactable = false;
                if (textSelectButton != null)
                    textSelectButton.text = messageSelected;
                break;
            }
        }
    }

    public virtual void OnClickSelect()
    {
        var savedCustomEquipments = PlayerSave.GetCustomEquipments();
        savedCustomEquipments[CustomEquipmentData.containerIndex] = indexInAvailableList;
        PlayerSave.SetCustomEquipments(savedCustomEquipments);
        list.UpdateSelectButtonsInteractable();
    }
}
