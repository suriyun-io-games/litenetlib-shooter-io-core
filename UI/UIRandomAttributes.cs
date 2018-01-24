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
        var dict = new Dictionary<CharacterAttributes, int>();
        var list = gameplay.attributes.Values.ToList();
        foreach (var entry in list)
        {
            dict.Add(entry, entry.randomWeight);
        }

        for (var i = 0; i < randomAttributes.Length; ++i)
        {
            var randomAttribute = randomAttributes[i];
            if (randomAttribute != null)
            {
                var randomedAttribute = WeightedRandomizer.From(dict).TakeOne();
                randomAttribute.SetAttribute(randomedAttribute);
                dict.Remove(randomedAttribute);
            }
        }
    }
}
