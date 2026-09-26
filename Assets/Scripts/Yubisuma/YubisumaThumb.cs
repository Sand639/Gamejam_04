using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// **手1つ分の親指。キーを押すと上がり、離すと下がる。**
///
/// 手のプレハブ（`Assets/Prefab/LeftHand` ／ `RightHand`）の一番上に付ける。
/// 手は握りこぶしのアニメーション（Hand_Idle）のままにしておき、
/// **上げるときだけ、親指の骨を上向きへ回して上書きする**。
///
///   ・下げている … アニメーションのまま（親指は横に寝ている）
///   ・上げている … 親指の付け根と先の骨を、手の上方向へ向ける
///
/// アニメーションが毎フレーム骨を元へ戻したあと（LateUpdate）で回すので、
/// 回した量が積み重なることはない。
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
    [Tooltip("この親指を上げるキー")]
    [SerializeField] private Key key = Key.A;

    [Tooltip("Hold：押している間だけ上がる ／ Toggle：押すたびに切り替わる")]
    [SerializeField] private RaiseMode mode = RaiseMode.Hold;

    [Header("骨")]
    [Tooltip("親指の付け根の骨（HandRig/Wrist/Hand/Thumb）")]
    [SerializeField] private Transform thumbRoot;

    [Tooltip("親指の真ん中の骨（Thumb2）。上げたときにまっすぐ伸ばす")]
    [SerializeField] private Transform thumbMiddle;

    [Tooltip("親指の先の骨（Thumb2 の下の _end）。向きを測るのに使う")]
    [SerializeField] private Transform thumbTip;

    [Header("見た目")]
    [Tooltip("上げたときに親指を向ける方向（手の向きを基準にした方向）。ふつうは真上（0, 1, 0）")]
    [SerializeField] private Vector3 raisedDirection = Vector3.up;

    [Tooltip("上げ下げの速さ。大きいほどすばやく動く")]
    [Range(1f, 60f)]
    [SerializeField] private float turnSpeed = 25f;

    /// <summary>いま上げているか。</summary>
    public bool IsRaised { get; private set; }

    /// <summary>
    /// 当てられて取り除かれたか。取り除かれた手は、キーを押しても動かず、本数にも数えない。
    /// </summary>
    public bool IsRemoved { get; private set; }

    /// <summary>この親指が付いている手（手ごと動かすときに使う）。</summary>
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

    /// <summary>0＝下げている、1＝上げている。途中の値でなめらかに動かす。</summary>
    private float raiseAmount;

    private Animator animator;
    private Quaternion baseRootRotation;
    private Quaternion baseMiddleRotation;

    /// <summary>この手を取り除く。以後は入力を受けず、本数にも数えない。</summary>
    public void Remove()
    {
        IsRemoved = true;
        IsRaised = false;
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        // アニメーションが無いときは、骨を元へ戻す係がいないので、元の向きを覚えておく
        if (thumbRoot != null)
        {
            baseRootRotation = thumbRoot.localRotation;
        }

        if (thumbMiddle != null)
        {
            baseMiddleRotation = thumbMiddle.localRotation;
        }
    }

    private void Update()
    {
        if (!IsRemoved)
        {
            IsRaised = ReadRaised();
        }

        float target = IsRaised ? 1f : 0f;
        raiseAmount = Mathf.Lerp(raiseAmount, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
    }

    private void LateUpdate()
    {
        if (thumbRoot == null || thumbTip == null)
        {
            return;
        }

        if (animator == null || !animator.isActiveAndEnabled)
        {
            thumbRoot.localRotation = baseRootRotation;

            if (thumbMiddle != null)
            {
                thumbMiddle.localRotation = baseMiddleRotation;
            }
        }

        if (raiseAmount < 0.001f)
        {
            return;
        }

        Vector3 up = transform.TransformDirection(raisedDirection).normalized;

        // 付け根を回して、親指全体を上へ向ける
        AimBone(thumbRoot, up);

        // 真ん中の骨も回して、曲がっている親指をまっすぐ伸ばす
        if (thumbMiddle != null)
        {
            AimBone(thumbMiddle, up);
        }
    }

    /// <summary>骨から親指の先へ向かう向きが、<paramref name="direction"/> に近づくように骨を回す。</summary>
    private void AimBone(Transform bone, Vector3 direction)
    {
        Vector3 current = thumbTip.position - bone.position;

        if (current.sqrMagnitude < 1e-8f)
        {
            return;
        }

        Quaternion full = Quaternion.FromToRotation(current, direction);
        bone.rotation = Quaternion.Slerp(Quaternion.identity, full, raiseAmount) * bone.rotation;
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
