using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class UIPickupWeapon : UIWeaponSelectEntry
{
    public Text ammoAmount;
    public PickupEntity pickupEntity;
    protected override void Update()
    {
        base.Update();
        weaponData = pickupEntity != null ? pickupEntity.weaponData : null;
        if (weaponData == null || weaponData.unlimitAmmo)
        {
            if (ammoAmount != null)
                ammoAmount.text = "";
        }
        else
        {
            if (ammoAmount != null)
                ammoAmount.text = pickupEntity.ammoAmount.ToString("N0");
        }
    }

    public override void OnClickSelectWeapon()
    {
        var localCharacter = BaseNetworkGameCharacter.Local as CharacterEntity;
        if (localCharacter == null || pickupEntity == null)
            return;
        localCharacter.CmdPickup(pickupEntity.netId);
    }
}
