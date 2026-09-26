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
                if (thumb != null && thumb.IsRaised)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
