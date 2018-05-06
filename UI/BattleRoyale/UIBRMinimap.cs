using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class UIBRMinimap : MonoBehaviour {

    public LayerMask mapLayerMask = 1;
    public RectTransform container;
    public RawImage uiMap;
    public Image uiPlayer;
    public Image uiCircle;
    public Image uiShrinkingCircle;
    public float objectScale;
    public Vector3 objectOffset;
    public Texture2D mapTexture { get; protected set; }

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

    private void Awake()
    {
        var rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        rt.Create();
        TempCamera.targetTexture = rt;
        TempCamera.cullingMask = mapLayerMask;

        container.pivot = Vector2.one;
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
        
        uiMap.texture = mapTexture;
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

    private void Update()
    {
        var localCharacter = BaseNetworkGameCharacter.Local;
        if (localCharacter != null)
        {
            var characterPosition = localCharacter.transform.position;
            var characterRotationY = localCharacter.transform.eulerAngles.y;
            var viewportPoint = TempCamera.WorldToViewportPoint(characterPosition);
            if (uiPlayer != null)
            {
                uiPlayer.rectTransform.localPosition = new Vector2(viewportPoint.x * container.rect.size.x, viewportPoint.y * container.rect.size.y) - container.rect.size;
                uiPlayer.rectTransform.eulerAngles = Vector3.forward * -characterRotationY;
            }
        }
    }
}
