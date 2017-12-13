using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSave
{
    public const string KeyPlayerName = "SavePlayerName";
    public const string KeyCharacter = "SaveSelectCharacter";
    public const string KeyHead = "SaveSelectHead";
    public const string KeyWeapons = "SaveSelectWeapons";

    public static string GetPlayerName()
    {
        if (!PlayerPrefs.HasKey(KeyPlayerName))
            SetPlayerName("Guest-" + string.Format("{0:0000}", Random.Range(1, 9999)));
        return PlayerPrefs.GetString(KeyPlayerName);
    }

    public static void SetPlayerName(string value)
    {
        PlayerPrefs.SetString(KeyPlayerName, value);
        PlayerPrefs.Save();
    }

    public static int GetCharacter()
    {
        return PlayerPrefs.GetInt(KeyCharacter, 0);
    }

    public static void SetCharacter(int value)
    {
        PlayerPrefs.SetInt(KeyCharacter, value);
        PlayerPrefs.Save();
    }

    public static int GetHead()
    {
        return PlayerPrefs.GetInt(KeyHead, 0);
    }

    public static void SetHead(int value)
    {
        PlayerPrefs.SetInt(KeyHead, value);
        PlayerPrefs.Save();
    }

    public static Dictionary<int, int> GetWeapons()
    {
        var data = PlayerPrefs.GetString(KeyWeapons);
        var splitedData = data.Split('|');
        Dictionary<int, int> weapons = new Dictionary<int, int>();
        foreach (var singleData in splitedData)
        {
            var splitedKeyPair = singleData.Split(':');
            if (splitedKeyPair.Length == 2)
                weapons.Add(int.Parse(splitedKeyPair[0]), int.Parse(splitedKeyPair[1]));
        }
        return weapons;
    }

    public static void SetWeapons(Dictionary<int, int> values)
    {
        var data = "";
        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(data))
                data += "|";
            data += value.Key + ":" + value.Value;
        }
        PlayerPrefs.SetString(KeyWeapons, data);
        PlayerPrefs.Save();
    }
}
