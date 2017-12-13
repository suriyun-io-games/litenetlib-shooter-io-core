using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BotData
{
    public string name;
    public int headDataIndex;
    public int characterDataIndex;
    public int[] weaponDataIndexes;

    public string GetSelectHead()
    {
        var headKeys = new List<string>(GameInstance.Heads.Keys);
        return headDataIndex < 0 || headDataIndex >= headKeys.Count ? headKeys[Random.Range(0, headKeys.Count)] : headKeys[headDataIndex];
    }

    public string GetSelectCharacter()
    {
        var characterKeys = new List<string>(GameInstance.Characters.Keys);
        return characterDataIndex < 0 || characterDataIndex >= characterKeys.Count ? characterKeys[Random.Range(0, characterKeys.Count)] : characterKeys[characterDataIndex];
    }

    public string GetSelectWeapons()
    {
        var selectedWeapons = new Dictionary<int, string>();
        foreach (var weaponDataIndex in weaponDataIndexes)
        {
            var weaponData = GetSelectWeapon(weaponDataIndex);
            selectedWeapons[weaponData.equipPosition] = weaponData.GetId();
        }
        var selectedWeaponIds = selectedWeapons.Values;
        var selectWeapons = "";
        foreach (var selectedWeaponId in selectedWeaponIds)
        {
            if (!string.IsNullOrEmpty(selectWeapons))
                selectWeapons += "|";
            selectWeapons += selectedWeaponId;
        }
        return selectWeapons;
    }

    public WeaponData GetSelectWeapon(int index)
    {
        var weaponKeys = new List<string>(GameInstance.Weapons.Keys);
        var key = index < 0 || index >= weaponKeys.Count ? weaponKeys[Random.Range(0, weaponKeys.Count)] : weaponKeys[index];
        return GameInstance.Weapons[key];
    }
}
