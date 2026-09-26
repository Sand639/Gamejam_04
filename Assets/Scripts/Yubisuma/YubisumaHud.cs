using System.Text;
using UnityEngine;

/// <summary>
/// **画面分割の上に、各プレイヤーの状態を出す仮の表示。**
///
/// ・それぞれの画面の上に、名前・キー・上げている本数
/// ・真ん中に仕切りの線
/// ・下の真ん中に、全員の合計（指スマで数を当てる対象）
///
/// 見た目は仮のもの（OnGUI）。本番の画面はあとで作り直す前提。
/// </summary>
public class YubisumaHud : MonoBehaviour
{
    [Tooltip("表示するプレイヤー")]
    [SerializeField] private YubisumaPlayer[] players = new YubisumaPlayer[0];

    [Tooltip("文字の大きさ（1920×1080 のときの値。窓が小さいと自動で縮む）")]
    [Range(0.5f, 4f)]
    [SerializeField] private float uiScale = 1.5f;

    [Tooltip("真ん中の仕切り線の太さ（ピクセル）")]
    [SerializeField] private float dividerWidth = 6f;

    [SerializeField] private Color dividerColor = new Color(0.1f, 0.1f, 0.1f);

    private GUIStyle labelStyle;
    private GUIStyle totalStyle;

    private void OnGUI()
    {
        EnsureStyles();

        float scale = Mathf.Max(0.5f, uiScale * Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
        labelStyle.fontSize = Mathf.RoundToInt(20 * scale);
        totalStyle.fontSize = Mathf.RoundToInt(28 * scale);

        int total = 0;

        foreach (YubisumaPlayer player in players)
        {
            if (player == null)
            {
                continue;
            }

            total += player.ShownRaisedCount;
            DrawPlayer(player, scale);
        }

        DrawDivider();

        float height = 50f * scale;
        GUI.Label(new Rect(0f, Screen.height - height - 10f * scale, Screen.width, height),
            $"合計 {total} 本", totalStyle);
    }

    /// <summary>そのプレイヤーの画面の左上に、状態を出す。</summary>
    private void DrawPlayer(YubisumaPlayer player, float scale)
    {
        Rect area = player.PlayerCamera != null
            ? player.PlayerCamera.pixelRect
            : new Rect(0f, 0f, Screen.width, Screen.height);

        // pixelRect は下が0、GUI は上が0 なので、上下をひっくり返す
        float top = Screen.height - area.yMax;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(player.DisplayName);
        builder.Append("キー：");

        YubisumaThumb[] thumbs = player.Thumbs;
        for (int i = 0; i < thumbs.Length; i++)
        {
            if (thumbs[i] == null)
            {
                continue;
            }

            // 「左／右」は画面の上での位置（手のモデルの左右ではない）
            builder.Append(i == 0 ? "左 " : " ／ 右 ").Append(thumbs[i].KeyName);
        }

        builder.AppendLine();
        builder.Append($"あげている：{player.ShownRaisedCount} 本");

        // 持っているスキル
        builder.AppendLine();
        builder.Append(player.HeldSkill == YubisumaSkillType.None
            ? "スキル：なし"
            : $"スキル：{YubisumaSkill.NameOf(player.HeldSkill)}（{player.SkillKeyName}）");

        // コンクリ／セメントで固定されている指
        StringBuilder locks = new StringBuilder();
        for (int i = 0; i < thumbs.Length; i++)
        {
            if (thumbs[i] != null && !thumbs[i].IsRemoved && thumbs[i].IsLocked)
            {
                locks.Append(locks.Length == 0 ? string.Empty : " ／ ")
                     .Append(i == 0 ? "左 " : "右 ")
                     .Append(thumbs[i].LockedRaised ? "上げたまま" : "下げたまま");
            }
        }

        if (locks.Length > 0)
        {
            builder.AppendLine();
            builder.Append($"固定：{locks}");
        }

        float margin = 16f * scale;
        GUI.Label(new Rect(area.x + margin, top + margin, area.width - margin * 2f, 200f * scale),
            builder.ToString(), labelStyle);
    }

    private void DrawDivider()
    {
        Color saved = GUI.color;
        GUI.color = dividerColor;
        GUI.DrawTexture(new Rect((Screen.width - dividerWidth) * 0.5f, 0f, dividerWidth, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = saved;
    }

    private void EnsureStyles()
    {
        if (labelStyle != null)
        {
            return;
        }

        // マウスが乗っても色が変わらないよう、どの状態の色もそろえる
        labelStyle = new GUIStyle(GUI.skin.label) { richText = false };
        YubisumaMatch.SetTextColor(labelStyle, Color.white);

        totalStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        YubisumaMatch.SetTextColor(totalStyle, Color.white);
    }
}
