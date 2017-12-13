using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct AttackAnimation
{
    [Range(0, 100)]
    public int actionId;
    public float animationDuration;
    public float launchDuration;
}
