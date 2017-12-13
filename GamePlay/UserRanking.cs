using UnityEngine.Networking;

public struct UserRanking
{
    public static readonly UserRanking Empty = new UserRanking();
    public NetworkInstanceId netId;
    public string playerName;
    public int score;
    public int killCount;
}