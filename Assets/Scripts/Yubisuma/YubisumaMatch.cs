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
///   ⑥ 手が無くなったプレイヤーが、**そのゲームを取る**
///   ⑦ **先に <see cref="winsToWinMatch"/> ゲーム取ったプレイヤーの勝ち**（初期値は2＝BO3）。
///      まだ決まらなければ、全員の手を元に戻し、**取られた側の番から**次のゲームを始める
///
/// ## スキル
///
///   ・試合の始めに、全員に**ランダムで1つ**配る
///   ・**自分の番で、スペースを押す前に**スキルのキー（1P：W ／ 2P：↑）で選ぶ（画面には出さない）。
///     「いっせーの ＜数字＞！」の代わりに「いっせーの ＜スキル名＞！」と出て使われ、**無くなる**
///   ・誰かが**当てたとき、当てていない側**はランダムで1つもらえる（**使わずに持っていたら、もらえない**）
///   ・使わなかったスキルは、次のゲームへ持ち越す
///   ・効果は <see cref="YubisumaSkillType"/> を読むこと。
///     コンクリ／セメントは「いっせーの ＜数字＞！」の瞬間の相手の指を固定し、**自分の次の番が来たら外す**
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

    [Tooltip("何ゲーム先に取ったら勝ちか。2 なら BO3（3ゲーム中2ゲーム先取）")]
    [Min(1)]
    [SerializeField] private int winsToWinMatch = 2;

    [Header("スキル")]
    [Tooltip("スキルを使うキー。Players と同じ順（1人目、2人目…）")]
    [SerializeField] private Key[] skillKeys = { Key.W, Key.UpArrow };

    [Tooltip("テスト用。None 以外にすると、配るスキルともらうスキルが全部これになる（ランダムにしない）。本番では None にしておく")]
    [SerializeField] private YubisumaSkillType testSkill = YubisumaSkillType.None;

    [Header("時間（秒）")]
    [Tooltip("「指スマスタート！」や「○ゲーム目」を出しておく時間")]
    [SerializeField] private float introSeconds = 1.5f;

    [Tooltip("ゲームを取ったあと、次のゲームを始めるまでの時間")]
    [SerializeField] private float gameResultSeconds = 2.5f;

    [Tooltip("「いっせーの」から「いっせーの ＜数字＞！」までの遅れ")]
    [SerializeField] private float callDelaySeconds = 1.5f;

    [Tooltip("「いっせーの ＜数字＞！」と結果を出しておく時間。この間は次のスペースを受け付けない")]
    [SerializeField] private float resultSeconds = 1.5f;

    [Header("当てたときの手の動き")]
    [Tooltip("手を下へ流す距離（m）。画面の外まで出る長さにする")]
    [SerializeField] private float scrollDistance = 8f;

    [Tooltip("手を下へ流すのにかける時間（秒）")]
    [SerializeField] private float scrollSeconds = 1.0f;

    [Header("音声")]
    [Tooltip("音声を鳴らす AudioSource。空なら、このオブジェクトに付いているものを使う")]
    [SerializeField] private AudioSource voiceSource;

    [Tooltip("「いっせーの」の声")]
    [SerializeField] private AudioClip isseenoVoice;

    [Tooltip("数字の声。0～4 の順に入れる（0番目が「0」）")]
    [SerializeField] private AudioClip[] numberVoices = new AudioClip[MaxCall + 1];

    [Header("表示")]
    [Tooltip("文字の大きさ（1920×1080 のときの値。窓が小さいと自動で縮む）")]
    [Range(0.5f, 4f)]
    [SerializeField] private float uiScale = 1.5f;

    /// <summary>指定できる本数の上限。これより大きい数字キーは 0 とみなす。</summary>
    private const int MaxCall = 4;

    private Phase phase = Phase.Intro;
    private int turnIndex;

    /// <summary>プレイヤーごとの、取ったゲームの数。</summary>
    private int[] wins = new int[0];

    /// <summary>いま何ゲーム目か（1から）。</summary>
    private int gameNumber = 1;

    /// <summary>最後に押された数字（0～4）。押されていなければ 0。</summary>
    private int calledNumber;

    /// <summary>この番で、番のプレイヤーが使ったスキル。使っていなければ None。</summary>
    private YubisumaSkillType activeSkill = YubisumaSkillType.None;

    /// <summary>
    /// プレイヤーごとに、コンクリ／セメントで**相手の指を固定しているか**。
    /// そのプレイヤーの次の番が来たら外す。
    /// </summary>
    private bool[] placedLock = new bool[0];

    private string bigMessage = string.Empty;
    private string subMessage = string.Empty;

    private GUIStyle bigStyle;
    private GUIStyle subStyle;
    private GUIStyle turnStyle;

    private YubisumaPlayer CurrentPlayer => players.Length > 0 ? players[turnIndex] : null;

    private IEnumerator Start()
    {
        PrepareVoices();
        wins = new int[players.Length];
        placedLock = new bool[players.Length];

        // スキルを1つずつ配る
        for (int i = 0; i < players.Length; i++)
        {
            players[i].HeldSkill = YubisumaSkill.Random(testSkill);
            players[i].SkillKeyName = i < skillKeys.Length ? KeyLabel(skillKeys[i]) : string.Empty;
            Debug.Log($"[指スマ] {players[i].DisplayName} にスキル「{YubisumaSkill.NameOf(players[i].HeldSkill)}」を配った");
        }

        phase = Phase.Intro;
        bigMessage = "指スマスタート！";
        subMessage = $"{winsToWinMatch}ゲーム先取";

        yield return new WaitForSeconds(introSeconds);

        yield return GameIntroRoutine();
    }

    /// <summary>「○ゲーム目」を出してから、番の指定を受け付け始める。</summary>
    private IEnumerator GameIntroRoutine()
    {
        phase = Phase.Intro;
        bigMessage = $"{gameNumber}ゲーム目";
        subMessage = string.Empty;

        yield return new WaitForSeconds(introSeconds);

        bigMessage = string.Empty;
        phase = Phase.WaitingCall;
    }

    /// <summary>
    /// <paramref name="winnerIndex"/> のプレイヤーがゲームを取ったときの流れ。
    /// 試合が決まれば止め、決まらなければ手を戻して次のゲームへ進む。
    /// </summary>
    private IEnumerator GameWonRoutine(int winnerIndex)
    {
        YubisumaPlayer winner = players[winnerIndex];
        wins[winnerIndex]++;

        Debug.Log($"[指スマ] {winner.DisplayName} が {gameNumber}ゲーム目を取った（{ScoreText()}）");

        if (wins[winnerIndex] >= winsToWinMatch)
        {
            phase = Phase.GameOver;
            bigMessage = $"{winner.DisplayName} の勝ち！";
            subMessage = ScoreText();
            yield break;
        }

        bigMessage = $"{winner.DisplayName} が {gameNumber}ゲーム目を取った！";
        subMessage = ScoreText();

        yield return new WaitForSeconds(gameResultSeconds);

        // 全員の手を両手とも元に戻す（スキルの固定も外れる）。
        // 持っているスキルはそのまま次のゲームへ持ち越す
        ForEachPlayer(player => player.RestoreHands());
        System.Array.Clear(placedLock, 0, placedLock.Length);
        activeSkill = YubisumaSkillType.None;

        // 次のゲームは、取られた側（勝った人の次の人）の番から始める
        turnIndex = (winnerIndex + 1) % players.Length;
        gameNumber++;

        yield return GameIntroRoutine();
    }

    /// <summary>「プレイヤー1 1 - 0 プレイヤー2」の形のスコア。3人以上なら「名前 勝ち数」を並べる。</summary>
    private string ScoreText()
    {
        if (players.Length == 2 && wins.Length == 2)
        {
            return $"{players[0].DisplayName}  {wins[0]} - {wins[1]}  {players[1].DisplayName}";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < players.Length && i < wins.Length; i++)
        {
            builder.Append(i == 0 ? string.Empty : " ／ ").Append($"{players[i].DisplayName} {wins[i]}");
        }

        return builder.ToString();
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

        if (phase == Phase.WaitingCall)
        {
            ReadSkillKeys(keyboard);
        }

        if (phase == Phase.WaitingCall && keyboard.spaceKey.wasPressedThisFrame)
        {
            StartCoroutine(CallRoutine());
        }
    }

    /// <summary>
    /// スキルのキーを読む。**番のプレイヤーだけが、スペースを押す前に選べる。**
    ///
    /// 選んでも画面には何も出さない（相手にばれないように）。
    /// 「いっせーの ＜数字＞！」の代わりに「いっせーの ＜スキル名＞！」と出た瞬間に使われ、無くなる。
    /// 選んだあとに**数字キーが押されたら取り消す**（その番は数字で宣言する）。スキルのキーを押し直しても選んだまま。
    /// </summary>
    private void ReadSkillKeys(Keyboard keyboard)
    {
        for (int i = 0; i < players.Length && i < skillKeys.Length; i++)
        {
            if (skillKeys[i] == Key.None || !keyboard[skillKeys[i]].wasPressedThisFrame)
            {
                continue;
            }

            YubisumaPlayer player = players[i];

            if (i != turnIndex)
            {
                subMessage = $"{player.DisplayName}：スキルは自分の番でだけ使えます";
                continue;
            }

            if (player.HeldSkill == YubisumaSkillType.None || activeSkill != YubisumaSkillType.None)
            {
                continue;
            }

            // 選ぶ。まだ持ったまま（「いっせーの ＜スキル名＞！」の瞬間に無くなる）。
            // 取り消しは、このあと数字キーが押されたとき（ReadNumberKeys）
            activeSkill = player.HeldSkill;
            Debug.Log($"[指スマ] {player.DisplayName} がスキル「{YubisumaSkill.NameOf(activeSkill)}」を選んだ");
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

                // スキルを選んだあとに数字が押されたら、スキルは取り消す（その番は数字で宣言する）
                if (activeSkill != YubisumaSkillType.None)
                {
                    Debug.Log($"[指スマ] 数字が押されたので、スキル「{YubisumaSkill.NameOf(activeSkill)}」を取り消した");
                    activeSkill = YubisumaSkillType.None;
                }
            }
        }
    }

    private IEnumerator CallRoutine()
    {
        phase = Phase.Calling;
        YubisumaPlayer caller = CurrentPlayer;

        bigMessage = "いっせーの";
        subMessage = string.Empty;
        PlayVoice(isseenoVoice);

        // 「いっせーの」の間は、全員の手を握りこぶしに戻す。
        // キーを押していても指は立たない（押しているかどうかは覚えておく）
        ForEachPlayer(player => player.HideHands());

        yield return new WaitForSeconds(callDelaySeconds);

        // 選んでいたスキルは、この瞬間に使われて無くなる
        YubisumaSkillType skill = activeSkill;
        activeSkill = YubisumaSkillType.None;

        if (skill != YubisumaSkillType.None)
        {
            caller.HeldSkill = YubisumaSkillType.None;
        }

        // 「せ！」の代わりに、番のプレイヤーが指定していた数字を出す（例：いっせーの 2！）。
        // スキルを選んでいたら、数字の代わりにスキル名を出す（例：いっせーの コンクリ！）。
        // スキル名の声はまだ無いので、そのときは声を鳴らさない
        if (skill == YubisumaSkillType.None)
        {
            bigMessage = $"いっせーの {calledNumber}！";
            PlayVoice(calledNumber < numberVoices.Length ? numberVoices[calledNumber] : null);
        }
        else
        {
            bigMessage = $"いっせーの {YubisumaSkill.NameOf(skill)}！";
        }

        // ★この瞬間にキーを押している手だけ、一斉に指を立てる。結果を出している間はその形で止める
        ForEachPlayer(player => player.RevealHands());

        // ★数字（スキル名）を出した瞬間に上がっている本数で決める（見えている指の本数と同じ）
        int total = CountRaised();

        // 当たりかどうか。イーブン・オッズを使っていれば偶数／奇数で決まる
        // ピースは2、サンダーは3を宣言したことになる（数字キーは関係ない）。それ以外は数字キーの数字
        int declared = YubisumaSkill.DeclaredNumberOf(skill, calledNumber);
        bool hit = YubisumaSkill.IsHit(skill, total, declared);

        // ピース（2）・サンダー（3）で当てたら、そのゲームにすぐ勝つ
        bool winsGame = hit && YubisumaSkill.WinsGameOnHit(skill);

        Debug.Log($"[指スマ] {caller.DisplayName} の番：宣言 {declared} ／ 上がっていた本数 {total} ／ スキル {YubisumaSkill.NameOf(skill)} → {(hit ? "当たり" : "はずれ")}{(winsGame ? "（ゲームに勝ち）" : string.Empty)}");

        System.Text.StringBuilder result = new System.Text.StringBuilder();

        // スキル名を出したときは数字が見えないので、宣言した数字も添える
        // （イーブン・オッズは数字を使わないので添えない）
        if (skill != YubisumaSkillType.None &&
            skill != YubisumaSkillType.Even && skill != YubisumaSkillType.Odds)
        {
            result.Append($"宣言 {declared} → ");
        }

        result.Append(hit ? $"{total}本！ {caller.DisplayName} 当たり！" : $"{total}本… はずれ");

        if (skill != YubisumaSkillType.None)
        {
            result.Append($"\n{YubisumaSkill.NameOf(skill)}：{YubisumaSkill.DescriptionOf(skill)}");
        }

        if (winsGame)
        {
            result.Append($"\n{YubisumaSkill.NameOf(skill)}成功！ このゲームは {caller.DisplayName} の勝ち");
        }

        // コンクリ／セメント：この瞬間の相手の指を固定する（自分の次の番が来たら外す）
        if (skill == YubisumaSkillType.Concrete || skill == YubisumaSkillType.Cement)
        {
            bool lockRaised = skill == YubisumaSkillType.Cement;
            int locked = 0;

            for (int i = 0; i < players.Length; i++)
            {
                if (i != turnIndex)
                {
                    locked += players[i].LockHands(lockRaised);
                }
            }

            placedLock[turnIndex] = true;
            result.Append($"\n{YubisumaSkill.NameOf(skill)}：相手の{(lockRaised ? "上がっている" : "下がっている")}指 {locked}本を固定");
        }

        if (winsGame)
        {
            // ピース／サンダーで勝ったときは、残っている手を全部いっしょに流して画面の外へ出す
            // （ふつうに勝つとき、最後の手が流れていくのと同じ見せ方）
            for (YubisumaThumb removed = caller.RemoveOneHand(); removed != null; removed = caller.RemoveOneHand())
            {
                StartCoroutine(ScrollOutAndHide(removed.Hand));
            }
        }
        else if (hit)
        {
            YubisumaThumb removed = caller.RemoveOneHand();

            if (removed != null)
            {
                StartCoroutine(ScrollOutAndHide(removed.Hand));
            }
        }

        // 当てたときは、当てていない側にスキルを1つ配る（使わずに持っていたら、もらえない）
        if (hit)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (i == turnIndex || players[i].HeldSkill != YubisumaSkillType.None)
                {
                    continue;
                }

                players[i].HeldSkill = YubisumaSkill.Random(testSkill);
                result.Append($"\n{players[i].DisplayName} はスキル「{YubisumaSkill.NameOf(players[i].HeldSkill)}」を手に入れた");
                Debug.Log($"[指スマ] {players[i].DisplayName} がスキル「{YubisumaSkill.NameOf(players[i].HeldSkill)}」を手に入れた");
            }
        }

        subMessage = result.ToString();

        yield return new WaitForSeconds(resultSeconds);

        if (winsGame || (hit && caller.RemainingHands == 0))
        {
            // このゲームを取った。先取数に届いたかは GameWonRoutine で決める
            yield return GameWonRoutine(turnIndex);
            yield break;
        }

        // 当たってもはずれても、いっせーのごとに番を交代する
        turnIndex = (turnIndex + 1) % players.Length;

        // その人がかけていた固定（コンクリ／セメント）は、その人の番が来たら外す
        if (placedLock[turnIndex])
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (i != turnIndex)
                {
                    players[i].UnlockHands();
                }
            }

            placedLock[turnIndex] = false;
        }

        // 手の見た目を、ふだんの動き（キーに合わせてすぐ動く）に戻す
        ForEachPlayer(player => player.ShowHandsLive());

        bigMessage = string.Empty;
        subMessage = string.Empty;
        phase = Phase.WaitingCall;
    }

    /// <summary>
    /// 音声を先に読み込んでおく。
    /// 鳴らす瞬間に読み込むと、**数字の声が指の立つ瞬間より遅れる**ことがあるため。
    /// </summary>
    private void PrepareVoices()
    {
        if (voiceSource == null)
        {
            voiceSource = GetComponent<AudioSource>();
        }

        if (isseenoVoice != null)
        {
            isseenoVoice.LoadAudioData();
        }

        foreach (AudioClip clip in numberVoices)
        {
            if (clip != null)
            {
                clip.LoadAudioData();
            }
        }
    }

    /// <summary>声を1つ鳴らす。前の声が残っていれば止めてから鳴らす（声が重ならないように）。</summary>
    private void PlayVoice(AudioClip clip)
    {
        if (voiceSource == null || clip == null)
        {
            return;
        }

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();
    }

    private void ForEachPlayer(System.Action<YubisumaPlayer> action)
    {
        foreach (YubisumaPlayer player in players)
        {
            if (player != null)
            {
                action(player);
            }
        }
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

    /// <summary>
    /// 手を下へ流して画面の外へ出し、最後に見えなくする（処理も止まる）。
    /// **次のゲームで元に戻すため、壊さずに非表示にしておく。**
    /// </summary>
    private IEnumerator ScrollOutAndHide(Transform hand)
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

        hand.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------
    // 表示（仮のもの）
    // ------------------------------------------------------------

    private void OnGUI()
    {
        EnsureStyles();

        float scale = Mathf.Max(0.5f, uiScale * Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
        bigStyle.fontSize = Mathf.RoundToInt(64 * scale);
        subStyle.fontSize = Mathf.RoundToInt(26 * scale);
        turnStyle.fontSize = Mathf.RoundToInt(26 * scale);

        // 下の真ん中（合計の表示の上）に、いまの番を出す。指定した数字は出さない。
        // 上に出すと、各プレイヤーの名前の表示と重なるため
        if (phase == Phase.WaitingCall || phase == Phase.Calling)
        {
            // スキルを持っていれば、使うキーも案内する（選んだかどうかで変えない。相手にばれないように）
            string skillHint = phase == Phase.WaitingCall &&
                               CurrentPlayer.HeldSkill != YubisumaSkillType.None
                ? $" ／ スキルは {CurrentPlayer.SkillKeyName}"
                : string.Empty;

            string turn = phase == Phase.WaitingCall
                ? $"{CurrentPlayer.DisplayName} の番（数字キー 0～4 で指定 → スペース{skillHint}）"
                : $"{CurrentPlayer.DisplayName} の番";

            DrawShadowed(new Rect(0f, Screen.height - 120f * scale, Screen.width, 50f * scale), turn, turnStyle);
        }

        // 番の表示のさらに上に、ゲームのスコアを出す
        if (wins.Length > 0 && phase != Phase.GameOver)
        {
            DrawShadowed(new Rect(0f, Screen.height - 170f * scale, Screen.width, 50f * scale),
                $"{ScoreText()}（{winsToWinMatch}ゲーム先取）", turnStyle);
        }

        if (!string.IsNullOrEmpty(bigMessage))
        {
            DrawShadowed(new Rect(0f, Screen.height * 0.5f - 60f * scale, Screen.width, 120f * scale),
                bigMessage, bigStyle);
        }

        if (!string.IsNullOrEmpty(subMessage))
        {
            // スキルの結果などで数行になることがあるので、上から詰めて描く
            DrawShadowed(new Rect(0f, Screen.height * 0.5f + 55f * scale, Screen.width, 200f * scale),
                subMessage, subStyle);
        }
    }

    /// <summary>画面の案内に出すキーの名前（矢印は記号にする）。</summary>
    private static string KeyLabel(Key key)
    {
        switch (key)
        {
            case Key.LeftArrow: return "←";
            case Key.RightArrow: return "→";
            case Key.UpArrow: return "↑";
            case Key.DownArrow: return "↓";
            default: return key.ToString();
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

        subStyle = new GUIStyle(bigStyle) { alignment = TextAnchor.UpperCenter };
        subStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);

        turnStyle = new GUIStyle(bigStyle) { fontStyle = FontStyle.Normal };
        turnStyle.normal.textColor = Color.white;
    }
}
