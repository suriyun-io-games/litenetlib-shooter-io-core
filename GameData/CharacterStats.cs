using LiteNetLib.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CharacterStats : INetSerializable
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

    public void Deserialize(NetDataReader reader)
    {
        addMaxHp = reader.GetPackedInt();
        addMaxArmor = reader.GetPackedInt();
        addMoveSpeed = reader.GetPackedInt();
        addWeaponDamageRate = reader.GetFloat();
        addReduceDamageRate = reader.GetFloat();
        addArmorReduceDamage = reader.GetFloat();
        addExpRate = reader.GetFloat();
        addScoreRate = reader.GetFloat();
        addHpRecoveryRate = reader.GetFloat();
        addArmorRecoveryRate = reader.GetFloat();
        addDamageRateLeechHp = reader.GetFloat();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPackedInt(addMaxHp);
        writer.PutPackedInt(addMaxArmor);
        writer.PutPackedInt(addMoveSpeed);
        writer.Put(addWeaponDamageRate);
        writer.Put(addReduceDamageRate);
        writer.Put(addArmorReduceDamage);
        writer.Put(addExpRate);
        writer.Put(addScoreRate);
        writer.Put(addHpRecoveryRate);
        writer.Put(addArmorRecoveryRate);
        writer.Put(addDamageRateLeechHp);
    }

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
