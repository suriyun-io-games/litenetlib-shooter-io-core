using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UIBase : MonoBehaviour
{
    public GameObject root;
    public bool hideOnAwake;
    public bool moveToLastSiblingOnShow;
    public UnityEvent onShow;
    public UnityEvent onHide;
    private bool isAwaken;

    private bool findUIExtension;
    private IUIExtension uiExtension;
    public IUIExtension UIExtension
    {
        get
        {
            if (!findUIExtension)
            {
                findUIExtension = true;
                uiExtension = GetComponent<IUIExtension>();
            }
            return uiExtension;
        }
    }

    protected virtual void Awake()
    {
        if (isAwaken)
            return;
        isAwaken = true;
        ValidateRoot();
        if (hideOnAwake)
        {
            if (onHide != null)
                onHide.Invoke();
            root.SetActive(false);
        }
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
        if (onShow != null)
            onShow.Invoke();
        if (moveToLastSiblingOnShow)
            root.transform.SetAsLastSibling();
        if (UIExtension == null)
            root.SetActive(true);
        else
            UIExtension.Show();
    }

    public virtual void Hide()
    {
        isAwaken = true;
        ValidateRoot();
        if (onHide != null)
            onHide.Invoke();
        if (UIExtension == null)
            root.SetActive(false);
        else
            UIExtension.Hide();
    }

    public virtual bool IsVisible()
    {
        ValidateRoot();
        return root.activeSelf;
    }
}
