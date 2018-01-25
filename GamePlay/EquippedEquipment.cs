using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public struct EquippedEquipment
{
    public static readonly EquippedEquipment Empty = new EquippedEquipment();
    public string equipmentId;
    private EquipmentData equipmentData;
    public EquipmentData EquipmentData
    {
        get
        {
            if (equipmentData == null && !string.IsNullOrEmpty(equipmentId))
                equipmentData = GameInstance.GetEquipment(equipmentId);
            return equipmentData;
        }
    }

    public bool ChangeEquipmentId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Equals(equipmentId))
            return false;
        equipmentData = null;
        equipmentId = id;
        return true;
    }

    public bool IsEmpty()
    {
        return Empty.Equals(this);
    }
}

public class SyncListEquippedEquipment : SyncListStruct<EquippedEquipment> { }
