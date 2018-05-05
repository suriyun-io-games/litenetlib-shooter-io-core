using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleRoyaleNetworkGameRule : IONetworkGameRule
{
    public override void OnUpdateCharacter(BaseNetworkGameCharacter character)
    {
        base.OnUpdateCharacter(character);
        var gameCharacter = character as CharacterEntity;
        if (gameCharacter.Hp <= 0)
        {
            IsMatchEnded = true;
            EndMatch();

            // Show ranking
        }
    }
}
