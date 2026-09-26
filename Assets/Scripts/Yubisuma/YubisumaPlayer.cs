using UnityEngine;

/// <summary>
/// **プレイヤー1人分。** 両手の親指をまとめて持ち、何本上げているかを数える。
///
/// 画面分割のため、プレイヤーごとに自分専用のカメラを持つ。
/// </summary>
public class YubisumaPlayer : MonoBehaviour
{
    [Tooltip("画面に出す名前")]
    [SerializeField] private string displayName = "プレイヤー1";

    [Tooltip("このプレイヤーの親指（左手・右手の順）")]
    [SerializeField] private YubisumaThumb[] thumbs = new YubisumaThumb[0];

    [Tooltip("このプレイヤーを映すカメラ。画面のどこに出すか（Viewport Rect）で左右を決める")]
    [SerializeField] private Camera playerCamera;

    public string DisplayName => displayName;

    public Camera PlayerCamera => playerCamera;

    public YubisumaThumb[] Thumbs => thumbs;

    /// <summary>いま上げている親指の本数。</summary>
    public int RaisedCount
    {
        get
        {
            int count = 0;

            foreach (YubisumaThumb thumb in thumbs)
            {
                if (IsAlive(thumb) && thumb.IsRaised)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// **見た目として**指を立てている本数（画面の表示用）。
    /// 「いっせーの」の間は、キーを押していても 0 になる（本数がばれないように）。
    /// </summary>
    public int ShownRaisedCount
    {
        get
        {
            int count = 0;

            foreach (YubisumaThumb thumb in thumbs)
            {
                if (IsAlive(thumb) && thumb.IsShownRaised)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>まだ残っている手の数。0になったら、このプレイヤーの勝ち。</summary>
    public int RemainingHands
    {
        get
        {
            int count = 0;

            foreach (YubisumaThumb thumb in thumbs)
            {
                if (IsAlive(thumb))
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// 手を1つ取り除く（当てたときに呼ぶ）。**右手から先に**取り除く。
    /// 取り除いた親指を返す。もう手が無ければ null。
    /// </summary>
    public YubisumaThumb RemoveOneHand()
    {
        for (int i = thumbs.Length - 1; i >= 0; i--)
        {
            if (IsAlive(thumbs[i]))
            {
                thumbs[i].Remove();
                return thumbs[i];
            }
        }

        return null;
    }

    /// <summary>取り除いた手も含めて、両手とも最初の状態に戻す（次のゲームを始めるとき）。</summary>
    public void RestoreHands()
    {
        foreach (YubisumaThumb thumb in thumbs)
        {
            if (thumb != null)
            {
                thumb.Restore();
            }
        }
    }

    /// <summary>残っている手を全部、握りこぶしの見た目にする（「いっせーの」の始まり）。</summary>
    public void HideHands()
    {
        foreach (YubisumaThumb thumb in thumbs)
        {
            if (IsAlive(thumb))
            {
                thumb.Hide();
            }
        }
    }

    /// <summary>残っている手を全部、いまのキーの状態で見せて止める（「いっせーの ＜数字＞！」の瞬間）。</summary>
    public void RevealHands()
    {
        foreach (YubisumaThumb thumb in thumbs)
        {
            if (IsAlive(thumb))
            {
                thumb.Reveal();
            }
        }
    }

    /// <summary>残っている手を全部、ふだんの動き（キーに合わせてすぐ動く）に戻す。</summary>
    public void ShowHandsLive()
    {
        foreach (YubisumaThumb thumb in thumbs)
        {
            if (IsAlive(thumb))
            {
                thumb.ShowLive();
            }
        }
    }

    private static bool IsAlive(YubisumaThumb thumb)
    {
        return thumb != null && !thumb.IsRemoved;
    }
}
