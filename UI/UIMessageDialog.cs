using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIMessageDialog : UIBase {
    public Text textMessage;
    public void Show(string message)
    {
        if (textMessage != null)
            textMessage.text = message;
        Show();
    }
}
