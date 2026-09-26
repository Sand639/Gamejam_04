using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// **指スマの試合の流れ。**
///
///   ① シーンが始まったら「指スマスタート！」を出す
///   ② 番のプレイヤーが**数字キー（0～4）**で本数を指定する（画面には出さない。最後に押した数字を覚えておく）
///   ③ **スペース**で「いっせーの」→ 少し遅れて「いっせーの ＜指定した数字＞！」
///   ④ 数字を出した瞬間に上がっている親指の合計が、指定した数と同じなら**当たり**。
///      番のプレイヤーの手を1つ、下へ流して画面の外へ出し、消す
///   ⑤ 当たり・はずれに関係なく、**いっせーのごとに番を交代**する
///   ⑥ 手が無くなったプレイヤーの勝ち
/// </summary>
public class YubisumaMatch : MonoBehaviour
{
    private enum Phase
    {
        /// <summary>「指スマスタート！」を出している</summary>
        Intro,

        /// <summary>番のプレイヤーの指定と、スペースを待っている</summary>
        WaitingCall,

        /// <summary>「いっせーの」～「いっせーの ＜数字＞！」～結果を出している</summary>
        Calling,

        /// <summary>勝負がついた</summary>
        GameOver,
    }

    [Tooltip("参加するプレイヤー。この順に番が回る")]
    [SerializeField] private YubisumaPlayer[] players = new YubisumaPlayer[0];

    [Header("時間（秒）")]
    [Tooltip("「指スマスタート！」を出しておく時間")]
    [SerializeField] private float introSeconds = 1.5f;

    [Tooltip("「いっせーの」から「いっせーの ＜数字＞！」までの遅れ")]
    [SerializeField] private float callDelaySeconds = 1.5f;

    [Tooltip("「いっせーの ＜数字＞！」と結果を出しておく時間。この間は次のスペースを受け付けない")]
    [SerializeField] private float resultSeconds = 1.5f;

    [Header("当てたときの手の動き")]
    [Tooltip("手を下へ流す距離（m）。画面の外まで出る長さにする")]
    [SerializeField] private float scrollDistance = 8f;

    [Tooltip("手を下へ流すのにかける時間（秒）")]
    [SerializeField] private float scrollSeconds = 1.0f;

    [Header("表示")]
    [Tooltip("文字の大きさ（1920×1080 のときの値。窓が小さいと自動で縮む）")]
    [Range(0.5f, 4f)]
    [SerializeField] private float uiScale = 1.5f;

    /// <summary>指定できる本数の上限。これより大きい数字キーは 0 とみなす。</summary>
    private const int MaxCall = 4;

    private Phase phase = Phase.Intro;
    private int turnIndex;

    /// <summary>最後に押された数字（0～4）。押されていなければ 0。</summary>
    private int calledNumber;

    private string bigMessage = string.Empty;
    private string subMessage = string.Empty;

    private GUIStyle bigStyle;
    private GUIStyle subStyle;
    private GUIStyle turnStyle;

    private YubisumaPlayer CurrentPlayer => players.Length > 0 ? players[turnIndex] : null;

    private IEnumerator Start()
    {
        phase = Phase.Intro;
        bigMessage = "指スマスタート！";

        yield return new WaitForSeconds(introSeconds);

        bigMessage = string.Empty;
        phase = Phase.WaitingCall;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        // 数字はいつでも受け付ける（「いっせーの」の最中に決め直してもよい）
        ReadNumberKeys(keyboard);

        if (phase == Phase.WaitingCall && keyboard.spaceKey.wasPressedThisFrame)
        {
            StartCoroutine(CallRoutine());
        }
    }

    /// <summary>
    /// 数字キーを読む。**最後に押された数字を覚えておく。**
    /// 0～4 はそのまま、5～9 は 0 にする。テンキーも使える。
    /// </summary>
    private void ReadNumberKeys(Keyboard keyboard)
    {
        for (int digit = 0; digit <= 9; digit++)
        {
            // キーの並びは 1～9 の次に 0 が来る（テンキーは 0～9 の順）
            Key digitKey = digit == 0 ? Key.Digit0 : Key.Digit1 + (digit - 1);

            if (keyboard[digitKey].wasPressedThisFrame ||
                keyboard[Key.Numpad0 + digit].wasPressedThisFrame)
            {
                calledNumber = digit <= MaxCall ? digit : 0;
                Debug.Log($"[指スマ] 指定：{calledNumber}（押したキー：{digit}）");
            }
        }
    }

    private IEnumerator CallRoutine()
    {
        phase = Phase.Calling;
        YubisumaPlayer caller = CurrentPlayer;

        bigMessage = "いっせーの";
        subMessage = string.Empty;

        yield return new WaitForSeconds(callDelaySeconds);

        // 「せ！」の代わりに、番のプレイヤーが指定していた数字を出す（例：いっせーの 2！）
        bigMessage = $"いっせーの {calledNumber}！";

        // ★数字を出した瞬間に上がっている本数で決める
        int total = CountRaised();
        bool hit = total == calledNumber;

        Debug.Log($"[指スマ] {caller.DisplayName} の番：指定 {calledNumber} ／ 上がっていた本数 {total} → {(hit ? "当たり" : "はずれ")}");

        subMessage = hit
            ? $"{total}本！ {caller.DisplayName} 当たり！"
            : $"{total}本… はずれ";

        if (hit)
        {
            YubisumaThumb removed = caller.RemoveOneHand();

            if (removed != null)
            {
                StartCoroutine(ScrollOutAndDestroy(removed.Hand));
            }
        }

        yield return new WaitForSeconds(resultSeconds);

        if (hit && caller.RemainingHands == 0)
        {
            phase = Phase.GameOver;
            bigMessage = $"{caller.DisplayName} の勝ち！";
            subMessage = string.Empty;
            yield break;
        }

        // 当たってもはずれても、いっせーのごとに番を交代する
        turnIndex = (turnIndex + 1) % players.Length;

        bigMessage = string.Empty;
        subMessage = string.Empty;
        phase = Phase.WaitingCall;
    }

    private int CountRaised()
    {
        int total = 0;

        foreach (YubisumaPlayer player in players)
        {
            if (player != null)
            {
                total += player.RaisedCount;
            }
        }

        return total;
    }

    /// <summary>手を下へ流して画面の外へ出し、最後に消す。</summary>
    private IEnumerator ScrollOutAndDestroy(Transform hand)
    {
        Vector3 start = hand.localPosition;
        Vector3 end = start + Vector3.down * scrollDistance;

        for (float time = 0f; time < scrollSeconds; time += Time.deltaTime)
        {
            // 最初はゆっくり、だんだん速く落ちていく
            float t = time / scrollSeconds;
            hand.localPosition = Vector3.Lerp(start, end, t * t);
            yield return null;
        }

        Destroy(hand.gameObject);
    }

    // ------------------------------------------------------------
    // 表示（仮のもの）
    // ------------------------------------------------------------

    private void OnGUI()
    {
        EnsureStyles();

        float scale = Mathf.Max(0.5f, uiScale * Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
        bigStyle.fontSize = Mathf.RoundToInt(64 * scale);
        subStyle.fontSize = Mathf.RoundToInt(32 * scale);
        turnStyle.fontSize = Mathf.RoundToInt(26 * scale);

        // 下の真ん中（合計の表示の上）に、いまの番を出す。指定した数字は出さない。
        // 上に出すと、各プレイヤーの名前の表示と重なるため
        if (phase == Phase.WaitingCall || phase == Phase.Calling)
        {
            string turn = phase == Phase.WaitingCall
                ? $"{CurrentPlayer.DisplayName} の番（数字キー 0～4 で指定 → スペース）"
                : $"{CurrentPlayer.DisplayName} の番";

            DrawShadowed(new Rect(0f, Screen.height - 120f * scale, Screen.width, 50f * scale), turn, turnStyle);
        }

        if (!string.IsNullOrEmpty(bigMessage))
        {
            DrawShadowed(new Rect(0f, Screen.height * 0.5f - 60f * scale, Screen.width, 120f * scale),
                bigMessage, bigStyle);
        }

        if (!string.IsNullOrEmpty(subMessage))
        {
            DrawShadowed(new Rect(0f, Screen.height * 0.5f + 60f * scale, Screen.width, 60f * scale),
                subMessage, subStyle);
        }
    }

    /// <summary>背景の色に負けないよう、黒い影を付けて描く。</summary>
    private static void DrawShadowed(Rect rect, string text, GUIStyle style)
    {
        Color saved = style.normal.textColor;

        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), text, style);

        style.normal.textColor = saved;
        GUI.Label(rect, text, style);
    }

    private void EnsureStyles()
    {
        if (bigStyle != null)
        {
            return;
        }

        bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        bigStyle.normal.textColor = Color.white;

        subStyle = new GUIStyle(bigStyle);
        subStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);

        turnStyle = new GUIStyle(bigStyle) { fontStyle = FontStyle.Normal };
        turnStyle.normal.textColor = Color.white;
    }
}
