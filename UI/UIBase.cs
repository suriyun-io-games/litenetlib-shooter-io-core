using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIBase : MonoBehaviour
{
    public GameObject root;
    public bool hideOnAwake;
    private bool isAwaken;

    protected virtual void Awake()
    {
        if (isAwaken)
            return;
        isAwaken = true;
        ValidateRoot();
        if (hideOnAwake)
            Hide();
    }

    public void ValidateRoot()
    {
        if (root == null)
            root = gameObject;
    }

    public virtual void Show()
    {
        isAwaken = true;
        ValidateRoot();
        root.SetActive(true);
    }

    public virtual void Hide()
    {
        isAwaken = true;
        ValidateRoot();
        root.SetActive(false);
    }

    public virtual bool IsVisible()
    {
        ValidateRoot();
        return root.activeSelf;
    }
}
