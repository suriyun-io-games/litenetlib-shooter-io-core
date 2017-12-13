using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UIBlackFade : MonoBehaviour
{
    public enum FadeState
    {
        None,
        FadeInDone,
        FadeOutDone
    }
    public CanvasGroup blackFade;
    public float fadeInSpeed = 5f;
    public float fadeOutSpeed = 3f;
    public bool prepareFadeOutOnStart;
    public bool fadeOutOnStart;
    public UnityEvent onFadeIn;
    public UnityEvent onFadeOut;
    public FadeState CurrentFadeState { get; protected set; }

    bool isFadeIn;
    bool isFadeOut;
    bool isInitFadeIn;
    bool isInitFadeOut;

    void Awake()
    {
        if (prepareFadeOutOnStart || fadeOutOnStart)
            blackFade.alpha = 1;
        else
            blackFade.alpha = 0;
    }

    void Start()
    {
        if (fadeOutOnStart)
            FadeOut();
    }

    void Update()
    {
        if (isFadeIn)
        {
            if (!isInitFadeIn)
            {
                blackFade.alpha = 0;
                blackFade.blocksRaycasts = true;
                isInitFadeIn = true;
            }
            UpdateFadeIn();
            isFadeOut = false;
        }

        if (isFadeOut)
        {
            if (!isInitFadeOut)
            {
                blackFade.alpha = 1;
                blackFade.blocksRaycasts = true;
                isInitFadeOut = true;
            }
            UpdateFadeOut();
            isFadeIn = false;
        }
    }

    void UpdateFadeIn()
    {
        // Lerp the colour of the texture between itself and black.
        blackFade.alpha = Mathf.Lerp(blackFade.alpha, 1, fadeInSpeed * Time.deltaTime);

        if (blackFade.alpha >= 0.95f)
        {
            blackFade.alpha = 1;
            if (onFadeIn != null)
                onFadeIn.Invoke();
            CurrentFadeState = FadeState.FadeInDone;
            isFadeIn = false;
        }
    }

    void UpdateFadeOut()
    {
        // Lerp the colour of the texture between itself and transparent.
        blackFade.alpha = Mathf.Lerp(blackFade.alpha, 0, fadeOutSpeed * Time.deltaTime);

        if (blackFade.alpha <= 0.05f)
        {
            blackFade.alpha = 0;
            if (onFadeOut != null)
                onFadeOut.Invoke();
            blackFade.blocksRaycasts = false;
            CurrentFadeState = FadeState.FadeOutDone;
            isFadeOut = false;
        }
    }

    public void FadeIn()
    {
        isInitFadeIn = false;
        isFadeIn = true;
        isFadeOut = false;
    }

    public void FadeOut()
    {
        isInitFadeOut = false;
        isFadeIn = false;
        isFadeOut = true;
    }
}
