using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class UINetworkClientError : MonoBehaviour
{
    public static UINetworkClientError Singleton { get; private set; }
    public UIMessageDialog messageDialog;
    public string roomFullMessage = "The room is full, try another room.";

    private void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        Singleton = this;
        BaseNetworkGameManager.onClientError += OnClientError;
    }

    public void OnClientError(int error)
    {
        if (messageDialog == null)
            return;

        switch ((NetworkError)error)
        {
            case NetworkError.NoResources:
                if (!string.IsNullOrEmpty(roomFullMessage))
                    messageDialog.Show(roomFullMessage);
                break;
        }
    }
}
