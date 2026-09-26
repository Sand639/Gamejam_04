using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// **手1つ分の「上げる指」。キーを押すと上がり、離すと下がる。**
///
/// 手のプレハブ（`Assets/Prefab/LeftHand` ／ `RightHand`）の一番上に付ける。
/// 見た目は手のアニメーションで切り替える。
///
///   ・下げている … 握りこぶし（Hand_Idle）
///   ・上げている … 中指を立てる（Hand_Fuck）
///
/// Animator の Bool パラメーター（初期値 `Raised`）を切り替えるだけなので、
/// ポーズを変えたいときはアニメーションを差し替えればよい。
///
/// **「キーを押しているか」と「見た目」は分けて持つ。**
/// 「いっせーの」の間は <see cref="Hide"/> で見た目を握りこぶしにしておき、
/// 「いっせーの ＜数字＞！」の瞬間に <see cref="Reveal"/> で一斉に指を立てる。
///
/// 上げ方は2通りから選べる。
///   ・Hold   … 押している間だけ上がる（指スマの実際の動きに近い）
///   ・Toggle … 1回押すと上がり、もう1回押すと下がる
/// </summary>
public class YubisumaThumb : MonoBehaviour
{
    public enum RaiseMode
    {
        /// <summary>押している間だけ上がる</summary>
        Hold,

        /// <summary>押すたびに上げる／下げるが切り替わる</summary>
        Toggle,
    }

    [Header("操作")]
    [Tooltip("この指を上げるキー")]
    [SerializeField] private Key key = Key.A;

    [Tooltip("Hold：押している間だけ上がる ／ Toggle：押すたびに切り替わる")]
    [SerializeField] private RaiseMode mode = RaiseMode.Hold;

    [Header("アニメーション")]
    [Tooltip("手の Animator。空なら、この手の中から自動で探す")]
    [SerializeField] private Animator animator;

    [Tooltip("上げているかを伝える Animator の Bool パラメーターの名前")]
    [SerializeField] private string raisedParameter = "Raised";

    /// <summary>いま上げているか。</summary>
    public bool IsRaised { get; private set; }

    /// <summary>
    /// 当てられて取り除かれたか。取り除かれた手は、キーを押しても動かず、本数にも数えない。
    /// </summary>
    public bool IsRemoved { get; private set; }

    /// <summary>この指が付いている手（手ごと動かすときに使う）。</summary>
    public Transform Hand => transform;

    /// <summary>画面の案内に出すキーの名前。</summary>
    public string KeyName
    {
        get
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
    }

    /// <summary>見た目（アニメーション）をどう出すか。</summary>
    public enum DisplayMode
    {
        /// <summary>キーに合わせてすぐ動く（ふだん）</summary>
        Live,

        /// <summary>キーを押していても握りこぶしのまま（「いっせーの」の間）</summary>
        Hidden,

        /// <summary><see cref="Reveal"/> した瞬間の形のまま止める（結果を出している間）</summary>
        Frozen,
    }

    /// <summary>いまの見た目の出し方。</summary>
    public DisplayMode Display { get; private set; } = DisplayMode.Live;

    private int raisedHash;
    private bool frozenRaised;

    /// <summary>最初に置かれていた位置。当てられて流れていった手を、次のゲームで戻すのに使う。</summary>
    private Vector3 homePosition;

    /// <summary>
    /// この手を取り除く。以後は入力を受けず、本数にも数えない。
    /// **見た目はそのまま残す**（立てた指のまま画面の外へ流れていくように）。
    /// </summary>
    public void Remove()
    {
        IsRemoved = true;
    }

    /// <summary>
    /// 取り除いた手を、**最初の位置・握りこぶしの形で元に戻す**（次のゲームを始めるとき）。
    /// </summary>
    public void Restore()
    {
        gameObject.SetActive(true);
        transform.localPosition = homePosition;

        IsRemoved = false;
        IsRaised = false;
        IsLocked = false;
        frozenRaised = false;
        Display = DisplayMode.Live;
        ApplyAnimation();
    }

    /// <summary>
    /// 見た目を握りこぶしに戻し、キーを押しても指を立てないようにする（「いっせーの」の始まり）。
    /// **キーを押しているかどうかは、見た目と関係なく覚えている。**
    /// </summary>
    public void Hide()
    {
        Display = DisplayMode.Hidden;
        ApplyAnimation();
    }

    /// <summary>
    /// いまキーを押していれば指を立て、**その形のまま止める**（「いっせーの ＜数字＞！」の瞬間）。
    /// 全員に同時に呼べば、指が一斉に立つ。
    /// </summary>
    public void Reveal()
    {
        frozenRaised = IsRaised;
        Display = DisplayMode.Frozen;
        ApplyAnimation();
    }

    /// <summary>ふだんの動き（キーに合わせてすぐ動く）に戻す。</summary>
    public void ShowLive()
    {
        Display = DisplayMode.Live;
        ApplyAnimation();
    }

    /// <summary>
    /// スキル（コンクリ／セメント）で固定されているか。
    /// 固定されている間は、キーを押しても離しても、指は <see cref="LockedRaised"/> のまま変わらない。
    /// </summary>
    public bool IsLocked { get; private set; }

    /// <summary>固定されている形。true＝上げたまま（セメント）、false＝下げたまま（コンクリ）。</summary>
    public bool LockedRaised { get; private set; }

    /// <summary>指をいまの形で固定する（スキル）。<paramref name="raised"/> が true なら上げたまま。</summary>
    public void Lock(bool raised)
    {
        IsLocked = true;
        LockedRaised = raised;
        IsRaised = raised;

        if (Display == DisplayMode.Live)
        {
            ApplyAnimation();
        }
    }

    /// <summary>固定を外す（キーで動かせるように戻す）。</summary>
    public void Unlock()
    {
        IsLocked = false;
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        raisedHash = Animator.StringToHash(raisedParameter);
        homePosition = transform.localPosition;
    }

    private void Update()
    {
        if (IsRemoved)
        {
            return;
        }

        bool wasRaised = IsRaised;
        IsRaised = IsLocked ? LockedRaised : ReadRaised();

        if (wasRaised != IsRaised && Display == DisplayMode.Live)
        {
            ApplyAnimation();
        }
    }

    /// <summary>見た目として指を立てているか（画面の本数の表示に使う）。</summary>
    public bool IsShownRaised
    {
        get
        {
            switch (Display)
            {
                case DisplayMode.Hidden: return false;
                case DisplayMode.Frozen: return frozenRaised;
                default: return IsRaised;
            }
        }
    }

    private void ApplyAnimation()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(raisedHash, IsShownRaised);
        }
    }

    private bool ReadRaised()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || key == Key.None)
        {
            return false;
        }

        var control = keyboard[key];

        return mode == RaiseMode.Hold
            ? control.isPressed
            : control.wasPressedThisFrame ? !IsRaised : IsRaised;
    }
}
