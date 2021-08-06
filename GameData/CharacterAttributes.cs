using UnityEngine;

public class CharacterAttributes : ScriptableObject
{
    public string title;
    [TextArea]
    public string description;
    public Texture icon;
    public int randomWeight;
    public CharacterStats stats;
    public int GetHashId()
    {
        return name.MakeHashId();
    }
}
