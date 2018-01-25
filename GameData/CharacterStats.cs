using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CharacterStats
{
    public int addMaxHp;
    public int addMaxArmor;
    public int addMoveSpeed;
    public float addWeaponDamageRate;
    public float addReduceDamageRate;
    public float addArmorReduceDamage;
    public float addExpRate;
    public float addScoreRate;
    public float addHpRecoveryRate;
    public float addArmorRecoveryRate;
    public float addDamageRateLeechHp;

    public static CharacterStats operator +(CharacterStats a, CharacterStats b)
    {
        var result = new CharacterStats();
        result.addMaxHp = a.addMaxHp + b.addMaxHp;
        result.addMaxArmor = a.addMaxArmor + b.addMaxArmor;
        result.addMoveSpeed = a.addMoveSpeed + b.addMoveSpeed;
        result.addWeaponDamageRate = a.addWeaponDamageRate + b.addWeaponDamageRate;
        result.addReduceDamageRate = a.addReduceDamageRate + b.addReduceDamageRate;
        result.addArmorReduceDamage = a.addArmorReduceDamage + b.addArmorReduceDamage;
        result.addExpRate = a.addExpRate + b.addExpRate;
        result.addScoreRate = a.addScoreRate + b.addScoreRate;
        result.addHpRecoveryRate = a.addHpRecoveryRate + b.addHpRecoveryRate;
        result.addArmorRecoveryRate = a.addArmorRecoveryRate + b.addArmorRecoveryRate;
        result.addDamageRateLeechHp = a.addDamageRateLeechHp + b.addDamageRateLeechHp;
        return result;
    }

    public static CharacterStats operator -(CharacterStats a, CharacterStats b)
    {
        var result = new CharacterStats();
        result.addMaxHp = a.addMaxHp - b.addMaxHp;
        result.addMaxArmor = a.addMaxArmor - b.addMaxArmor;
        result.addMoveSpeed = a.addMoveSpeed - b.addMoveSpeed;
        result.addWeaponDamageRate = a.addWeaponDamageRate - b.addWeaponDamageRate;
        result.addReduceDamageRate = a.addReduceDamageRate - b.addReduceDamageRate;
        result.addArmorReduceDamage = a.addArmorReduceDamage - b.addArmorReduceDamage;
        result.addExpRate = a.addExpRate - b.addExpRate;
        result.addScoreRate = a.addScoreRate - b.addScoreRate;
        result.addHpRecoveryRate = a.addHpRecoveryRate - b.addHpRecoveryRate;
        result.addArmorRecoveryRate = a.addArmorRecoveryRate - b.addArmorRecoveryRate;
        result.addDamageRateLeechHp = a.addDamageRateLeechHp - b.addDamageRateLeechHp;
        return result;
    }
}
