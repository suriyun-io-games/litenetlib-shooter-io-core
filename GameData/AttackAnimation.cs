using UnityEngine;

[System.Serializable]
public class AttackAnimation
{
    [Range(0, 100)]
    public int actionId;
    public float animationDuration;
    public float launchDuration;
    public float speed = 1f;
    public bool isAnimationForLeftHandWeapon;
}
