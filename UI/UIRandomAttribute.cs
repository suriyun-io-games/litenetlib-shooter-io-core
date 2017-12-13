using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIRandomAttribute : MonoBehaviour
{
    public Text textTitle;
    public Text textDescription;
    public RawImage icon;
    public Animator animator;
    public string animatorStateName;
    [HideInInspector]
    public UIGameplay uiGameplay;
    private CharacterAttributes _attributes;
    public void SetAttribute(CharacterAttributes attributes)
    {
        _attributes = attributes;
        if (textTitle != null)
            textTitle.text = attributes.title;
        if (textDescription != null)
            textDescription.text = attributes.description;
        if (icon != null)
            icon.texture = attributes.icon;
        if (animator != null)
            animator.Play(animatorStateName);
    }

    public void OnClickAddAttribute()
    {
        uiGameplay.AddAttribute(_attributes.name);
    }
}
