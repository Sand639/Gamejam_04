using UnityEngine;

/// <summary>指スマのスキルの種類。</summary>
public enum YubisumaSkillType
{
    /// <summary>持っていない</summary>
    None,

    /// <summary>コンクリ：下がっている相手の指を、次の自分のターンまで上げられなくする</summary>
    Concrete,

    /// <summary>セメント：上がっている相手の指を、次の自分のターンまで下げられなくする</summary>
    Cement,

    /// <summary>イーブン：本数が偶数なら当たり</summary>
    Even,

    /// <summary>オッズ：本数が奇数なら当たり</summary>
    Odds,

    /// <summary>ピース：2を宣言して当たったら、そのゲームに勝つ</summary>
    Piece,

    /// <summary>サンダー：3を宣言して当たったら、そのゲームに勝つ</summary>
    Thunder,
}

/// <summary>
/// **スキルの名前・説明・配り方をまとめたところ。**
///
/// スキルの効果そのもの（判定や指の固定）は <see cref="YubisumaMatch"/> が行う。
/// </summary>
public static class YubisumaSkill
{
    /// <summary>配られるスキル（None 以外）。</summary>
    private static readonly YubisumaSkillType[] All =
    {
        YubisumaSkillType.Concrete,
        YubisumaSkillType.Cement,
        YubisumaSkillType.Even,
        YubisumaSkillType.Odds,
        YubisumaSkillType.Piece,
        YubisumaSkillType.Thunder,
    };

    /// <summary>
    /// ランダムに1つ選ぶ。
    /// <paramref name="fixedSkill"/> が None 以外なら、ランダムにせずそれを返す（テスト用）。
    /// </summary>
    public static YubisumaSkillType Random(YubisumaSkillType fixedSkill = YubisumaSkillType.None)
    {
        if (fixedSkill != YubisumaSkillType.None)
        {
            return fixedSkill;
        }

        return All[UnityEngine.Random.Range(0, All.Length)];
    }

    /// <summary>画面に出す名前。</summary>
    public static string NameOf(YubisumaSkillType skill)
    {
        switch (skill)
        {
            case YubisumaSkillType.Concrete: return "コンクリ";
            case YubisumaSkillType.Cement: return "セメント";
            case YubisumaSkillType.Even: return "イーブン";
            case YubisumaSkillType.Odds: return "オッズ";
            case YubisumaSkillType.Piece: return "ピース";
            case YubisumaSkillType.Thunder: return "サンダー";
            default: return "なし";
        }
    }

    /// <summary>効果の説明（企画の表と同じ文）。</summary>
    public static string DescriptionOf(YubisumaSkillType skill)
    {
        switch (skill)
        {
            case YubisumaSkillType.Concrete: return "下がっている相手の指を次の自分のターンまで上げられなくする";
            case YubisumaSkillType.Cement: return "上がっている相手の指を次の自分のターンまで下げられなくする";
            case YubisumaSkillType.Even: return "手数が偶数なら得点";
            case YubisumaSkillType.Odds: return "手数が奇数なら得点";
            case YubisumaSkillType.Piece: return "2を宣言して的中した場合勝利する";
            case YubisumaSkillType.Thunder: return "3を宣言して的中した場合勝利する";
            default: return string.Empty;
        }
    }

    /// <summary>画面の下の「持っているスキル」の欄に出す、短い説明。</summary>
    public static string ShortDescriptionOf(YubisumaSkillType skill)
    {
        switch (skill)
        {
            case YubisumaSkillType.Concrete: return "下げた指が次の自分の手番まで固定";
            case YubisumaSkillType.Cement: return "上げた指が次の自分の手番まで固定";
            case YubisumaSkillType.Even: return "偶数の宣言";
            case YubisumaSkillType.Odds: return "奇数の宣言";
            case YubisumaSkillType.Piece: return "宣言2で通れば勝ち";
            case YubisumaSkillType.Thunder: return "宣言3で通れば勝ち";
            default: return string.Empty;
        }
    }

    /// <summary>
    /// このスキルを使った番に**宣言したことになる数字**。
    /// ピースは2、サンダーは3を宣言するスキルなので、数字キーに関係なくその数字になる。
    /// それ以外は、数字キーで指定していた数字のまま。
    /// </summary>
    public static int DeclaredNumberOf(YubisumaSkillType skill, int calledNumber)
    {
        switch (skill)
        {
            case YubisumaSkillType.Piece: return 2;
            case YubisumaSkillType.Thunder: return 3;
            default: return calledNumber;
        }
    }

    /// <summary>
    /// このスキルを使った番が当たりかどうか。
    /// イーブン・オッズは本数の偶数／奇数で、それ以外は「宣言した数字と同じか」で決める。
    /// </summary>
    public static bool IsHit(YubisumaSkillType skill, int total, int calledNumber)
    {
        switch (skill)
        {
            case YubisumaSkillType.Even: return total % 2 == 0;
            case YubisumaSkillType.Odds: return total % 2 == 1;
            default: return total == calledNumber;
        }
    }

    /// <summary>
    /// 当たったとき、そのゲームにすぐ勝つか（ピース・サンダー）。
    /// 宣言する数字は <see cref="DeclaredNumberOf"/> で自動で決まる（ピースは2、サンダーは3）。
    /// </summary>
    public static bool WinsGameOnHit(YubisumaSkillType skill)
    {
        return skill == YubisumaSkillType.Piece || skill == YubisumaSkillType.Thunder;
    }
}
