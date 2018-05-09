using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class BRMiniMapCamera : MonoBehaviour
{
    public static BRMiniMapCamera Singleton { get; private set; }
    private Camera tempCamera;
    public Camera TempCamera
    {
        get
        {
            if (tempCamera == null)
                tempCamera = GetComponent<Camera>();
            return tempCamera;
        }
    }

    public Texture2D mapTexture { get; protected set; }

    public void Awake()
    {
        Singleton = this;

        var rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        rt.Create();
        TempCamera.targetTexture = rt;
    }

    private void Start()
    {
        StartCoroutine(CaptureMap());
    }

    IEnumerator CaptureMap()
    {
        yield return new WaitForEndOfFrame();
        mapTexture = RenderTextureToTexture2D(TempCamera.targetTexture);
        TempCamera.enabled = false;
    }

    public Texture2D RenderTextureToTexture2D(RenderTexture fromTexture)
    {
        var tempRt = RenderTexture.active;

        var toTexture = new Texture2D(fromTexture.width, fromTexture.height);
        RenderTexture.active = fromTexture;
        toTexture.ReadPixels(new Rect(0, 0, fromTexture.width, fromTexture.height), 0, 0);
        toTexture.Apply();

        RenderTexture.active = tempRt;

        return toTexture;
    }
}
