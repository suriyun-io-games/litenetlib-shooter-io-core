using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UIRandomAttributes : MonoBehaviour {
    public UIRandomAttribute[] randomAttributes;
    public UIGameplay uiGameplay
    {
        set
        {
            for (var i = 0; i < randomAttributes.Length; ++i)
            {
                var randomAttribute = randomAttributes[i];
                if (randomAttribute != null)
                    randomAttribute.uiGameplay = value;
            }
        }
    }

    public void Random()
    {
        var gameplay = GameplayManager.Singleton;
        var list = gameplay.attributes.Values.ToList();
        list.Shuffle();
        var j = 0;
        for (var i = 0; i < randomAttributes.Length; ++i)
        {
            var randomAttribute = randomAttributes[i];
            if (randomAttribute != null)
            {
                var attribute = list[j];
                randomAttribute.SetAttribute(attribute);
                ++j;
            }
        }
    }
}
