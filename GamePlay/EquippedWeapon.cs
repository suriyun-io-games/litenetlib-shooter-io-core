using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public struct EquippedWeapon
{
    public static readonly EquippedWeapon Empty = new EquippedWeapon();
    [HideInInspector]
    public string defaultId;
    public string weaponId;
    public int currentAmmo;
    public int currentReserveAmmo;
    private WeaponData weaponData;
    public WeaponData WeaponData
    {
        get
        {
            if (weaponData == null && !string.IsNullOrEmpty(weaponId))
                weaponData = GameInstance.GetWeapon(weaponId);
            return weaponData;
        }
    }

    public bool ChangeWeaponId(string id, int ammoAmount)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        var tempWeaponData = GameInstance.GetWeapon(id);
        if (tempWeaponData == null)
            return false;
        var maxAmmo = tempWeaponData.maxAmmo;
        var maxReserveAmmo = tempWeaponData.maxReserveAmmo;
        if (!id.Equals(weaponId))
        {
            currentAmmo = ammoAmount;
            currentReserveAmmo = 0;
        }
        else
        {
            // Increase reserve ammo if it's same weapon
            currentReserveAmmo += ammoAmount;
            // If reserve ammo are full, don't pick up weapon
            if (currentReserveAmmo == maxReserveAmmo)
                return false;
        }
        weaponData = null;
        weaponId = id;
        if (currentAmmo > maxAmmo)
        {
            AddReserveAmmo(currentAmmo - maxAmmo);
            currentAmmo = maxAmmo;
        }
        if (currentReserveAmmo > maxReserveAmmo)
            currentReserveAmmo = maxReserveAmmo;
        return true;
    }

    public void SetMaxAmmo()
    {
        if (WeaponData == null)
            return;
        currentAmmo = WeaponData.maxAmmo;
        currentReserveAmmo = WeaponData.maxReserveAmmo;
    }

    public bool AddReserveAmmo(int amount)
    {
        if (WeaponData == null || currentReserveAmmo == WeaponData.maxReserveAmmo)
            return false;
        currentReserveAmmo += amount;
        if (currentReserveAmmo > WeaponData.maxReserveAmmo)
            currentReserveAmmo = WeaponData.maxReserveAmmo;
        return true;
    }

    public void Reload()
    {
        if (WeaponData == null)
            return;

        if (WeaponData.reloadOneAmmoAtATime)
        {
            currentAmmo += 1;
            currentReserveAmmo -= 1;
        }
        else
        {
            var returnReserveAmmo = currentAmmo;
            if (currentAmmo + currentReserveAmmo <= WeaponData.maxAmmo)
            {
                currentAmmo += currentReserveAmmo;
                currentReserveAmmo = 0;
                returnReserveAmmo = 0;
            }
            else
            {
                if (currentReserveAmmo >= WeaponData.maxAmmo)
                {
                    currentReserveAmmo -= WeaponData.maxAmmo;
                    currentAmmo = WeaponData.maxAmmo;
                }
                else
                {
                    currentAmmo += currentReserveAmmo;
                    if (currentAmmo >= WeaponData.maxAmmo)
                    {
                        returnReserveAmmo = currentAmmo - WeaponData.maxAmmo;
                        currentAmmo = WeaponData.maxAmmo;
                    }
                    currentReserveAmmo = 0;
                }
            }
            AddReserveAmmo(returnReserveAmmo);
        }
    }

    public void DecreaseAmmo(int amount = 1)
    {
        if (WeaponData == null || WeaponData.unlimitAmmo)
            return;
        if (currentAmmo >= amount)
            currentAmmo -= amount;
    }

    public bool CanShoot()
    {
        return WeaponData != null && (currentAmmo > 0 || WeaponData.unlimitAmmo);
    }

    public bool CanReload()
    {
        return WeaponData != null && currentAmmo < WeaponData.maxAmmo && currentReserveAmmo > 0 && !WeaponData.unlimitAmmo;
    }

    public bool IsEmpty()
    {
        return Empty.Equals(this);
    }
}

public class SyncListEquippedWeapon : SyncListStruct<EquippedWeapon> { }
