using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIWeaponSelectList : UIBase
{
    public int equipPosition;
    public UIWeaponSelectEntry prefab;
    public Transform container;
    private readonly Dictionary<string, UIWeaponSelectEntry> UIs = new Dictionary<string, UIWeaponSelectEntry>();

    public override void Show()
    {
        base.Show();
        SetupList();
    }

    public void SetEquipPosition(int equipPosition)
    {
        this.equipPosition = equipPosition;
        SetupList();
    }

    public void SetupList()
    {
        ClearWeapons();
        var weapons = GameInstance.AvailableWeapons;
        for (var i = 0; i < weapons.Count; ++i)
        {
            var weapon = weapons[i];
            AddWeapon(i, weapon);
        }
    }

    public void UpdateSelectButtonsInteractable()
    {
        foreach (var ui in UIs)
        {
            ui.Value.UpdateSelectButtonInteractable();
        }
    }

    public void AddWeapon(int indexInAvailableList, WeaponData weaponData)
    {
        if (weaponData == null || UIs.ContainsKey(weaponData.GetId()) || weaponData.equipPosition != equipPosition)
            return;
        var uiObject = Instantiate(prefab.gameObject);
        uiObject.SetActive(true);
        uiObject.transform.SetParent(container, false);
        var ui = uiObject.GetComponent<UIWeaponSelectEntry>();
        ui.weaponData = weaponData;
        ui.indexInAvailableList = indexInAvailableList;
        ui.list = this;
        ui.UpdateSelectButtonInteractable();
        UIs[weaponData.GetId()] = ui;
    }

    public bool RemoveWeapon(string id)
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

    public void ClearWeapons()
    {
        UIs.Clear();
        for (var i = 0; i < container.childCount; ++i)
        {
            var child = container.GetChild(i);
            Destroy(child.gameObject);
        }
    }
}
