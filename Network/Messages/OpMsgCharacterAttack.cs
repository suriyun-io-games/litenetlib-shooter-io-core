using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib.Utils;

public class OpMsgCharacterAttack : BaseOpMsg
{
    public override ushort OpId
    {
        get
        {
            return 11001;
        }
    }

    public int weaponId;
    public bool isLeftHandWeapon;
    public Vector3 targetPosition;
    public uint attackerNetId;
    public float addRotationX;
    public float addRotationY;

    public override void Deserialize(NetDataReader reader)
    {
        weaponId = reader.GetInt();
        isLeftHandWeapon = reader.GetBool();
        targetPosition = reader.GetVector3();
        attackerNetId = reader.GetPackedUInt();
        addRotationX = reader.GetFloat();
        addRotationY = reader.GetFloat();
    }

    public override void Serialize(NetDataWriter writer)
    {
        writer.Put(weaponId);
        writer.Put(isLeftHandWeapon);
        writer.PutVector3(targetPosition);
        writer.PutPackedUInt(attackerNetId);
        writer.Put(addRotationX);
        writer.Put(addRotationY);
    }
}
