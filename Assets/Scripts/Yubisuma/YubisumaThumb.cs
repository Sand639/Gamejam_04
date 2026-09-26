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

    private int raisedHash;

    /// <summary>この手を取り除く。以後は入力を受けず、本数にも数えない。</summary>
    public void Remove()
    {
        IsRemoved = true;
        IsRaised = false;
        ApplyAnimation();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        raisedHash = Animator.StringToHash(raisedParameter);
    }

    private void Update()
    {
        if (IsRemoved)
        {
            return;
        }

        bool wasRaised = IsRaised;
        IsRaised = ReadRaised();

        if (wasRaised != IsRaised)
        {
            ApplyAnimation();
        }
    }

    private void ApplyAnimation()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(raisedHash, IsRaised);
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
