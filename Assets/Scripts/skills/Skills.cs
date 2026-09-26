using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;


public struct SkillFlag
{
    public bool IsUsed;
}

public struct Skill
{
    public SkillFlag Concrete;
    public SkillFlag Cement;
    public SkillFlag Even;
    public SkillFlag Odds;
    public SkillFlag Piece;
    public SkillFlag Thunder;
}

public class Skills
{
    private Skill skill;

    public Skills()
    {
        skill.Concrete = new SkillFlag { IsUsed = false };
        skill.Cement = new SkillFlag { IsUsed = false };
        skill.Even = new SkillFlag { IsUsed = false };
        skill.Odds = new SkillFlag { IsUsed = false };
        skill.Piece = new SkillFlag { IsUsed = false };
        skill.Thunder = new SkillFlag { IsUsed = false };
    }

    public void ResetConcrete()
    {
        skill.Concrete.IsUsed = false;
    }

    public void ResetCement()
    {
        skill.Cement.IsUsed = false;
    }

    public void ResetEven()
    {
        skill.Even.IsUsed = false;
    }

    public void ResetOdds()
    {
        skill.Odds.IsUsed = false;
    }

    public void ResetPiece()
    {
        skill.Piece.IsUsed = false;
    }

    public void ResetThunder()
    {
        skill.Thunder.IsUsed = false;
    }

    // Concrete
    // 下がっている相手の指を、次の自分のターンまで上げられなくする
    public bool UseConcrete(int opponentFingerCount)
    {
        skill.Concrete.IsUsed = true;

        if (opponentFingerCount >= 2) return false;

        return true;
    }

    // Cement
    // 上がっている相手の指を、次の自分のターンまで下げられなくする
    public bool UseCement(int opponentFingerCount)
    {
        skill.Cement.IsUsed = true;

        if (opponentFingerCount <= 0) return false;

        return true;
    }

    // Even
    // 手数が偶数なら得点
    public bool UseEven(int fingerCount)
    {
        skill.Even.IsUsed = true;

        if (fingerCount % 2 == 0)
            return true;

        return false;
    }

    // Odds
    // 手数が奇数なら得点
    public bool UseOdds(int fingerCount)
    {
        skill.Odds.IsUsed = true;

        if (fingerCount % 2 == 1)
            return true;

        return false;
    }

    // Piece
    // 2を宣言して命中した場合勝利
    public bool UsePiece(int fingerCount)
    {
        skill.Piece.IsUsed = true;

        if (fingerCount == 2)
            return true;

        return false;
    }

    // Thunder
    // 3を宣言して命中した場合勝利
    public bool UseThunder(int fingerCount)
    {
        skill.Thunder.IsUsed = true;

        if (fingerCount == 3)
            return true;

        return false;
    }
}
