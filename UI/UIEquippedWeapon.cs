using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIEquippedWeapon : UIWeaponSelectEntry
{
    public GameObject ammoAmountContainer;
    public Text currentAmmo;
    public Text currentReserveAmmo;
    public EquippedWeapon equippedWeapon;
    protected override void Update()
    {
        base.Update();
        weaponData = equippedWeapon.WeaponData;
        if (weaponData == null || weaponData.unlimitAmmo)
        {
            if (currentAmmo != null)
                currentAmmo.text = "";
            if (currentReserveAmmo != null)
                currentReserveAmmo.text = "";
            if (ammoAmountContainer != null)
                ammoAmountContainer.SetActive(false);
        }
        else
        {
            if (currentAmmo != null)
                currentAmmo.text = equippedWeapon.currentAmmo.ToString("N0");
            if (currentReserveAmmo != null)
                currentReserveAmmo.text = equippedWeapon.currentReserveAmmo.ToString("N0");
            if (ammoAmountContainer != null)
                ammoAmountContainer.SetActive(true);
        }
    }

    public override void OnClickSelectWeapon()
    {
        var localCharacter = BaseNetworkGameCharacter.Local as CharacterEntity;
        if (localCharacter == null)
            return;
        localCharacter.CmdChangeWeapon(indexInAvailableList);
    }
}
