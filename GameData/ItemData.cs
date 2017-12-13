using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ItemData : InGameProductData
{
    [Tooltip("If this is true, player have to buy this item to unlock and able to use.")]
    public bool isLock;
    public CharacterStats stats;

    public virtual bool IsUnlock()
    {
        return !isLock || IsBought();
    }

    public override bool CanBuy()
    {
        canBuyOnlyOnce = true;
        return base.CanBuy();
    }
}
