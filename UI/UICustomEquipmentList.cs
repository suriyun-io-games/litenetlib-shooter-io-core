using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UICustomEquipmentList : UIBase
{
    public int containerIndex;
    public UICustomEquipmentEntry prefab;
    public Transform container;
    private readonly Dictionary<string, UICustomEquipmentEntry> UIs = new Dictionary<string, UICustomEquipmentEntry>();

    public override void Show()
    {
        base.Show();
        SetupList();
    }

    public void SetEquipPosition(int equipPosition)
    {
        this.containerIndex = equipPosition;
        SetupList();
    }

    public void SetupList()
    {
        ClearCustomEquipments();
        var customEquipments = GameInstance.AvailableCustomEquipments;
        for (var i = 0; i < customEquipments.Count; ++i)
        {
            var customEquipment = customEquipments[i];
            AddCustomEquipment(i, customEquipment);
        }
    }

    public void UpdateSelectButtonsInteractable()
    {
        foreach (var ui in UIs)
        {
            ui.Value.UpdateSelectButtonInteractable();
        }
    }

    public void AddCustomEquipment(int indexInAvailableList, CustomEquipmentData customEquipmentData)
    {
        if (customEquipmentData == null || UIs.ContainsKey(customEquipmentData.GetId()) || customEquipmentData.containerIndex != containerIndex)
            return;
        var uiObject = Instantiate(prefab.gameObject);
        uiObject.SetActive(true);
        uiObject.transform.SetParent(container, false);
        var ui = uiObject.GetComponent<UICustomEquipmentEntry>();
        ui.customEquipmentData = customEquipmentData;
        ui.indexInAvailableList = indexInAvailableList;
        ui.list = this;
        ui.UpdateSelectButtonInteractable();
        UIs[customEquipmentData.GetId()] = ui;
    }

    public bool RemoveCustomEquipment(string id)
    {
        if (string.IsNullOrEmpty(id) || !UIs.ContainsKey(id))
            return false;
        var ui = UIs[id];
        if (UIs.Remove(id))
        {
            Destroy(ui.gameObject);
            return true;
        }
        return false;
    }

    public void ClearCustomEquipments()
    {
        UIs.Clear();
        for (var i = 0; i < container.childCount; ++i)
        {
            var child = container.GetChild(i);
            Destroy(child.gameObject);
        }
    }
}
