using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class BotData
{
    public string name;
    public int headDataIndex;
    public int characterDataIndex;
    public int[] weaponDataIndexes;

    public int GetSelectHead()
    {
        var headKeys = new List<int>(GameInstance.Heads.Keys);
        if (headKeys.Count == 0)
            return 0;
        return headDataIndex < 0 || headDataIndex >= headKeys.Count ? headKeys[Random.Range(0, headKeys.Count)] : headKeys[headDataIndex];
    }

    public int GetSelectCharacter()
    {
        var characterKeys = new List<int>(GameInstance.Characters.Keys);
        if (characterKeys.Count == 0)
            return 0;
        return characterDataIndex < 0 || characterDataIndex >= characterKeys.Count ? characterKeys[Random.Range(0, characterKeys.Count)] : characterKeys[characterDataIndex];
    }

    public int[] GetSelectWeapons()
    {
        var selectedWeapons = new Dictionary<int, int>();
        foreach (var weaponDataIndex in weaponDataIndexes)
        {
            var weaponData = GetSelectWeapon(weaponDataIndex);
            if (weaponData != null)
                selectedWeapons[weaponData.equipPosition] = weaponData.GetHashId();
        }
        return selectedWeapons.Values.ToArray();
    }

    public WeaponData GetSelectWeapon(int index)
    {
        var weaponKeys = new List<int>(GameInstance.Weapons.Keys);
        if (weaponKeys.Count == 0)
            return null;
        var key = index < 0 || index >= weaponKeys.Count ? weaponKeys[Random.Range(0, weaponKeys.Count)] : weaponKeys[index];
        return GameInstance.Weapons[key];
    }
}
