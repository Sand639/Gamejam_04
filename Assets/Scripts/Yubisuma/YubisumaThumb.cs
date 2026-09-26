using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// **親指1本分。キーを押すと上がり、離すと下がる。**
///
/// 親指の付け根（このオブジェクト）を回して、上げ下げを見せる。
/// 子に親指の見た目を置いておくこと。
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

    [Header("見た目")]
    [Tooltip("下げているときの傾き（度）。手の内側へ倒す。左手はマイナス、右手はプラス")]
    [SerializeField] private float loweredAngle = -80f;

    [Tooltip("上げ下げの速さ。大きいほどすばやく動く")]
    [Range(1f, 60f)]
    [SerializeField] private float turnSpeed = 25f;

    [Tooltip("親指の見た目。上げているときに色を変える")]
    [SerializeField] private Renderer thumbRenderer;

    [SerializeField] private Color loweredColor = new Color(0.95f, 0.78f, 0.62f);
    [SerializeField] private Color raisedColor = new Color(1f, 0.85f, 0.2f);

    /// <summary>いま上げているか。</summary>
    public bool IsRaised { get; private set; }

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

    private void Start()
    {
        ApplyColor();
        transform.localRotation = TargetRotation();
    }

    private void Update()
    {
        bool wasRaised = IsRaised;
        IsRaised = ReadRaised();

        if (wasRaised != IsRaised)
        {
            ApplyColor();
        }

        // 目標の角度へなめらかに近づける（パッと切り替わるより、上げたことが分かりやすい）
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation, TargetRotation(), 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
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

    private Quaternion TargetRotation()
    {
        return Quaternion.Euler(0f, 0f, IsRaised ? 0f : loweredAngle);
    }

    private void ApplyColor()
    {
        if (thumbRenderer != null)
        {
            thumbRenderer.material.color = IsRaised ? raisedColor : loweredColor;
        }
    }
}
