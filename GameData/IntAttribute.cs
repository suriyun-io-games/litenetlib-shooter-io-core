using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IntAttribute
{
    public int minValue;
    public int maxValue;
    public float growth;

    public int Calculate(int currentLevel, int maxLevel)
    {
        if (currentLevel <= 0)
            currentLevel = 1;
        if (maxLevel <= 0)
            maxLevel = 1;
        if (currentLevel > maxLevel)
            currentLevel = maxLevel;
        return minValue + Mathf.CeilToInt((maxValue - minValue) * Mathf.Pow((float)(currentLevel - 1) / (float)(maxLevel - 1), growth));
    }
}
