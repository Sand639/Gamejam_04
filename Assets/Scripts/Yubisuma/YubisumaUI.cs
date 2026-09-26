using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// **指スマの画面の UI。**（企画のモックの見た目）
///
///   ・プレイヤーごとの枠と、名前（1P は左上、2P は右上）。番のプレイヤーには「▶ ～ の番」
///   ・取ったゲームの数を**丸の画像**で出す（取ったら黒い丸）
///   ・手の下に、その手のキーの画像。スキルで固定されていれば「固定」
///   ・画面の下に「skill ＋ キー」と、**持っているスキルと短い説明**
///   ・真ん中に、ギザギザの吹き出しで「いっせーの 2！」など
///
/// UI は**再生を始めたときにコードで組み立てる**（シーンに置くのはこのオブジェクトだけ）。
/// 画像（キー・丸・吹き出し）はインスペクターで差し替えられる。
///
/// これがあるときは、前の文字だけの表示（YubisumaHud と YubisumaMatch の OnGUI）は出さない。
/// </summary>
public class YubisumaUI : MonoBehaviour
{
    /// <summary>この UI が出ているか。出ていれば、前の文字だけの表示は出さない。</summary>
    public static bool IsShowing { get; private set; }

    /// <summary>UI の部品を置く層（Unity の標準の「UI」レイヤー）。</summary>
    private const int UiLayer = 5;

    [Serializable]
    public struct KeySprite
    {
        public Key key;
        public Sprite sprite;
    }

    [Tooltip("試合の流れ。ここから名前・勝ち数・スキル・大きな文字などを読む")]
    [SerializeField] private YubisumaMatch match;

    [Header("画像")]
    [Tooltip("キーの画像（Assets/Sprites/Key）。キーごとに1つ")]
    [SerializeField] private KeySprite[] keySprites = new KeySprite[0];

    [Tooltip("まだ取っていないゲームの丸")]
    [SerializeField] private Sprite winEmpty;

    [Tooltip("取ったゲームの丸")]
    [SerializeField] private Sprite winFilled;

    [Tooltip("真ん中の吹き出し")]
    [SerializeField] private Sprite burst;

    [Tooltip("文字のフォント。空なら Unity の標準のフォント")]
    [SerializeField] private Font font;

    [Header("色")]
    [Tooltip("プレイヤーごとの背景の色（カメラの背景に使う）")]
    [SerializeField] private Color[] panelColors =
    {
        new Color(0.46f, 0.77f, 1.00f),
        new Color(0.96f, 0.63f, 0.56f),
    };

    [SerializeField] private Color frameColor = new Color(0.38f, 0.38f, 0.38f);
    [SerializeField] private Color textColor = new Color(0.12f, 0.12f, 0.12f);
    [SerializeField] private Color turnColor = new Color(0.85f, 0.18f, 0.15f);
    [SerializeField] private Color skillBoxColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);

    [Header("大きさ（1920×1080 のときの値）")]
    [SerializeField] private float keyIconSize = 64f;
    [SerializeField] private float winMarkSize = 72f;

    [Tooltip("手の下のキーの画像を、手の一番下からどれだけ下に出すか")]
    [SerializeField] private float keyIconBelowHand = 16f;

    [Header("吹き出しの出方")]
    [Tooltip("吹き出しが小さい状態から大きく出てくるまでの時間（秒）")]
    [SerializeField] private float popSeconds = 0.25f;

    [Tooltip("出てくるとき、どれだけ大きく行きすぎてから戻るか（0 なら行きすぎない）")]
    [SerializeField] private float popOvershoot = 2.2f;

    [Tooltip("数字（スキル名）を出すときの吹き出しの大きさ（ふだんの吹き出しに対する倍率）")]
    [SerializeField] private float revealScale = 1.6f;

    // ------------------------------------------------------------

    private class HandView
    {
        public YubisumaThumb hand;
        public Renderer renderer;
        public Transform palm;
        public Camera camera;
        public RectTransform icon;
        public Text lockLabel;
    }

    private class PlayerView
    {
        public YubisumaPlayer player;
        public Text name;
        public Text hint;
        public Image[] winMarks;
        public Image skillKey;
        public Text skillText;
        public readonly List<HandView> hands = new List<HandView>();
    }

    private readonly List<PlayerView> views = new List<PlayerView>();
    private readonly Dictionary<Key, Sprite> keyLookup = new Dictionary<Key, Sprite>();

    private RectTransform canvasRect;
    private Canvas canvas;
    private GameObject burstObject;
    private Text bigText;
    private GameObject subObject;
    private Text subText;
    private Text noticeText;

    private void OnEnable()
    {
        IsShowing = true;
    }

    private void OnDisable()
    {
        IsShowing = false;
    }

    private void Start()
    {
        if (match == null)
        {
            match = FindFirstObjectByType<YubisumaMatch>();
        }

        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        foreach (KeySprite entry in keySprites)
        {
            keyLookup[entry.key] = entry.sprite;
        }

        Build();
    }

    // ------------------------------------------------------------
    // 組み立て
    // ------------------------------------------------------------

    private void Build()
    {
        GameObject canvasObject = new GameObject("YubisumaCanvas", typeof(RectTransform));
        canvasObject.layer = UiLayer;
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvasObject.transform;

        YubisumaPlayer[] players = match != null ? match.Players : new YubisumaPlayer[0];

        for (int i = 0; i < players.Length; i++)
        {
            views.Add(BuildPlayer(i, players[i]));
        }

        // 真ん中の吹き出しと、その下の結果の文字
        burstObject = NewImage("Burst", canvasRect, burst, Color.white).gameObject;
        burstObject.GetComponent<Image>().preserveAspect = true;
        RectTransform burstRect = (RectTransform)burstObject.transform;
        burstRect.sizeDelta = new Vector2(520f, 380f);
        burstRect.anchoredPosition = new Vector2(0f, 40f);
        bigText = NewText("BigText", burstRect, 60, TextAnchor.MiddleCenter, textColor, FontStyle.Bold);
        Stretch(bigText.rectTransform, 70f, 60f);
        bigText.resizeTextForBestFit = true;
        bigText.verticalOverflow = VerticalWrapMode.Truncate;
        bigText.resizeTextMinSize = 24;
        bigText.resizeTextMaxSize = 150;

        Image subBox = NewImage("SubBox", canvasRect, null, new Color(0f, 0f, 0f, 0.55f));
        subObject = subBox.gameObject;
        RectTransform subRect = subBox.rectTransform;
        subRect.sizeDelta = new Vector2(860f, 150f);
        subRect.anchoredPosition = new Vector2(0f, -250f);
        subText = NewText("SubText", subRect, 32, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Stretch(subText.rectTransform, 20f, 10f);
        subText.resizeTextForBestFit = true;
        subText.verticalOverflow = VerticalWrapMode.Truncate;
        subText.resizeTextMinSize = 16;
        subText.resizeTextMaxSize = 34;

        // 上の真ん中：モザイクを切り替えたときの文字
        noticeText = NewText("Notice", canvasRect, 28, TextAnchor.UpperCenter, textColor, FontStyle.Bold);
        AnchorTop(noticeText.rectTransform, 0.5f, -24f, new Vector2(900f, 50f));
    }

    private PlayerView BuildPlayer(int index, YubisumaPlayer player)
    {
        PlayerView view = new PlayerView { player = player };
        Camera camera = player.PlayerCamera;

        // カメラの背景の色をモックの色にする
        if (camera != null && index < panelColors.Length)
        {
            camera.backgroundColor = panelColors[index];
        }

        // そのプレイヤーの画面の範囲（カメラの Viewport Rect と同じ）
        Rect area = camera != null ? camera.rect : new Rect(index * 0.5f, 0f, 0.5f, 1f);
        RectTransform half = NewRect($"Player{index + 1}Panel", canvasRect);
        half.anchorMin = new Vector2(area.xMin, area.yMin);
        half.anchorMax = new Vector2(area.xMax, area.yMax);
        half.offsetMin = half.offsetMax = Vector2.zero;

        BuildFrame(half);

        bool left = area.center.x < 0.5f;

        // 名前（1P は左上、2P は右上）
        view.name = NewText("Name", half, 44, left ? TextAnchor.UpperLeft : TextAnchor.UpperRight, textColor, FontStyle.Bold);
        view.name.supportRichText = true;
        AnchorTop(view.name.rectTransform, left ? 0f : 1f, -22f, new Vector2(700f, 60f));
        view.name.rectTransform.anchoredPosition += new Vector2(left ? 34f : -34f, 0f);

        // 勝ち数の丸の下：番のときだけ、操作の案内
        view.hint = NewText("Hint", half, 26, TextAnchor.UpperCenter, turnColor, FontStyle.Bold);
        AnchorTop(view.hint.rectTransform, 0.5f, -182f, new Vector2(800f, 40f));

        // 取ったゲームの丸（上の真ん中）
        int marks = match != null ? match.WinsToWinMatch : 2;
        view.winMarks = new Image[marks];
        float gap = 18f;
        float total = marks * winMarkSize + (marks - 1) * gap;
        for (int m = 0; m < marks; m++)
        {
            Image mark = NewImage($"Win{m + 1}", half, winEmpty, Color.white);
            mark.preserveAspect = true;
            AnchorTop(mark.rectTransform, 0.5f, -95f, new Vector2(winMarkSize, winMarkSize));
            mark.rectTransform.anchoredPosition += new Vector2(-total / 2f + winMarkSize / 2f + m * (winMarkSize + gap), 0f);
            view.winMarks[m] = mark;
        }

        // 画面の下：skill ＋ キー ＋ 持っているスキル
        Image box = NewImage("SkillBox", half, null, skillBoxColor);
        RectTransform boxRect = box.rectTransform;
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.sizeDelta = new Vector2(640f, 74f);
        boxRect.anchoredPosition = new Vector2(0f, 52f);
        view.skillText = NewText("SkillText", boxRect, 30, TextAnchor.MiddleCenter, textColor, FontStyle.Bold);
        Stretch(view.skillText.rectTransform, 16f, 4f);
        view.skillText.resizeTextForBestFit = true;
        view.skillText.verticalOverflow = VerticalWrapMode.Truncate;
        view.skillText.resizeTextMinSize = 16;
        view.skillText.resizeTextMaxSize = 30;

        Text skillLabel = NewText("SkillLabel", boxRect, 40, TextAnchor.LowerLeft, textColor, FontStyle.Italic);
        skillLabel.text = "skill";
        RectTransform labelRect = skillLabel.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0f);
        labelRect.sizeDelta = new Vector2(100f, 56f);
        labelRect.anchoredPosition = new Vector2(-30f, -6f);

        view.skillKey = NewImage("SkillKey", boxRect, SpriteOf(match != null ? match.SkillKeyOf(index) : Key.None), Color.white);
        view.skillKey.preserveAspect = true;
        RectTransform skillKeyRect = view.skillKey.rectTransform;
        skillKeyRect.anchorMin = skillKeyRect.anchorMax = new Vector2(0f, 1f);
        skillKeyRect.pivot = new Vector2(0f, 0f);
        skillKeyRect.sizeDelta = new Vector2(52f, 52f);
        skillKeyRect.anchoredPosition = new Vector2(66f, -2f);

        // 手の下のキーの画像
        foreach (YubisumaThumb hand in player.Thumbs)
        {
            if (hand == null)
            {
                continue;
            }

            HandView handView = new HandView
            {
                hand = hand,
                renderer = hand.GetComponentInChildren<SkinnedMeshRenderer>(),
                palm = FindDeep(hand.transform, "Hand") ?? hand.transform,
                camera = camera,
            };

            Image icon = NewImage($"Key_{hand.name}", canvasRect, SpriteOf(hand.BoundKey), Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.sizeDelta = new Vector2(keyIconSize, keyIconSize);
            handView.icon = icon.rectTransform;

            handView.lockLabel = NewText("Lock", handView.icon, 24, TextAnchor.UpperCenter, turnColor, FontStyle.Bold);
            RectTransform lockRect = handView.lockLabel.rectTransform;
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(0.5f, 0f);
            lockRect.pivot = new Vector2(0.5f, 1f);
            lockRect.sizeDelta = new Vector2(200f, 32f);
            lockRect.anchoredPosition = new Vector2(0f, -2f);

            view.hands.Add(handView);
        }

        return view;
    }

    /// <summary>プレイヤーの画面のふちに、細い枠を描く。</summary>
    private void BuildFrame(RectTransform half)
    {
        const float inset = 10f;
        const float thickness = 5f;

        // 上・下・左・右
        MakeBar(half, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(inset, -inset - thickness), new Vector2(-inset, -inset));
        MakeBar(half, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(inset, inset), new Vector2(-inset, inset + thickness));
        MakeBar(half, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(inset, inset), new Vector2(inset + thickness, -inset));
        MakeBar(half, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-inset - thickness, inset), new Vector2(-inset, -inset));
    }

    private void MakeBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Image bar = NewImage("Frame", parent, null, frameColor);
        RectTransform rect = bar.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    // ------------------------------------------------------------
    // 毎フレームの更新
    // ------------------------------------------------------------

    private void LateUpdate()
    {
        if (match == null || canvasRect == null)
        {
            return;
        }

        for (int i = 0; i < views.Count; i++)
        {
            UpdatePlayer(i, views[i]);
        }

        UpdateBurst(match.BigMessage);

        string sub = match.SubMessage;
        subObject.SetActive(!string.IsNullOrEmpty(sub));
        subText.text = sub;

        noticeText.text = match.MosaicNotice;
    }

    /// <summary>
    /// 真ん中の吹き出し。**文字が変わるたびに、小さい状態からバンっと大きく出す。**
    ///
    ///   ・「いっせーの」             … ふだんの大きさの吹き出し
    ///   ・「いっせーの 2！」のとき   … 「いっせーの」を消し、**もっと大きな吹き出しに「2！」だけ**を出す
    ///                                   （スキルなら「サンダー！」など）
    ///   ・そのほか（指スマスタート！・1ゲーム目・勝利など）… ふだんの大きさ
    /// </summary>
    private void UpdateBurst(string message)
    {
        const string Prefix = "いっせーの ";

        bool isReveal = !string.IsNullOrEmpty(message) && message.StartsWith(Prefix);
        string shown = isReveal ? message.Substring(Prefix.Length) : message;

        burstObject.SetActive(!string.IsNullOrEmpty(shown));

        if (shown != lastBurstText)
        {
            lastBurstText = shown;
            bigText.text = shown;
            popStartTime = Time.unscaledTime;
            burstTargetScale = isReveal ? revealScale : 1f;
        }

        // 小さい状態から、少し大きく行きすぎてから戻る（バンっと出る感じ）
        float t = popSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - popStartTime) / popSeconds) : 1f;
        float c1 = popOvershoot;
        float c3 = c1 + 1f;
        float eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        float scale = Mathf.Lerp(0.15f, burstTargetScale, eased);
        burstObject.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private string lastBurstText = string.Empty;
    private float popStartTime = -10f;
    private float burstTargetScale = 1f;

    private void UpdatePlayer(int index, PlayerView view)
    {
        bool myTurn = !match.IsGameOver && match.TurnIndex == index;
        string hex = ColorUtility.ToHtmlStringRGB(turnColor);
        view.hint.text = myTurn && match.IsWaitingCall
            ? "数字キー 0～4（0 は Q でも）で宣言 → スペース"
            : string.Empty;

        view.name.text = myTurn
            ? $"<color=#{hex}>▶</color> {view.player.DisplayName} <size=30><color=#{hex}>の番</color></size>"
            : view.player.DisplayName;

        int wins = match.WinsOf(index);
        for (int m = 0; m < view.winMarks.Length; m++)
        {
            view.winMarks[m].sprite = m < wins ? winFilled : winEmpty;
        }

        YubisumaSkillType skill = view.player.HeldSkill;
        view.skillText.text = skill == YubisumaSkillType.None
            ? "なし"
            : $"{YubisumaSkill.NameOf(skill)}：{YubisumaSkill.ShortDescriptionOf(skill)}";

        foreach (HandView hand in view.hands)
        {
            UpdateHand(hand);
        }
    }

    /// <summary>キーの画像を、手の一番下のすぐ下へ動かす。</summary>
    private void UpdateHand(HandView view)
    {
        bool visible = view.hand != null && view.hand.gameObject.activeInHierarchy && !view.hand.IsRemoved &&
                       view.renderer != null && view.camera != null;

        view.icon.gameObject.SetActive(visible);

        if (!visible)
        {
            return;
        }

        // 手の一番下（手首）の真ん中を、画面の位置に直す
        Bounds bounds = view.renderer.bounds;
        Vector3 bottom = new Vector3(view.palm.position.x, bounds.min.y, view.palm.position.z);
        Vector3 screen = view.camera.WorldToScreenPoint(bottom);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 local))
        {
            view.icon.anchorMin = view.icon.anchorMax = new Vector2(0.5f, 0.5f);
            view.icon.anchoredPosition = local + new Vector2(0f, -keyIconBelowHand - keyIconSize / 2f);
        }

        view.lockLabel.text = view.hand.IsLocked
            ? (view.hand.LockedRaised ? "固定（上げたまま）" : "固定（下げたまま）")
            : string.Empty;
    }

    // ------------------------------------------------------------
    // 補助
    // ------------------------------------------------------------

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private Sprite SpriteOf(Key key)
    {
        return keyLookup.TryGetValue(key, out Sprite sprite) ? sprite : null;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text NewText(string name, Transform parent, int size, TextAnchor anchor, Color color, FontStyle style)
    {
        RectTransform rect = NewRect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void Stretch(RectTransform rect, float padX, float padY)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padX, padY);
        rect.offsetMax = new Vector2(-padX, -padY);
    }

    /// <summary>親の上の端（横は anchorX）を基準に置く。</summary>
    private static void AnchorTop(RectTransform rect, float anchorX, float y, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 1f);
        rect.pivot = new Vector2(anchorX, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(0f, y);
    }
}
