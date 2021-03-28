using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectActivationByViewMode : MonoBehaviour
{
    public CharacterEntity.ViewMode viewMode;
    public GameObject[] objects;

    void Update()
    {
        if (BaseNetworkGameCharacter.Local == null)
            return;
        foreach (var obj in objects)
        {
            obj.SetActive((BaseNetworkGameCharacter.Local as CharacterEntity).viewMode == viewMode);
        }
    }
}
