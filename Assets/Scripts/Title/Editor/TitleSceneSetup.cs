using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// **タイトル画面のシーン**を作るツール。
///
/// Unityのメニュー「Tools > Gamejam04 > タイトル画面のシーンを作る」から実行できる。
///
/// ・タイトルの文字と「スタート」ボタンだけの画面
/// ・スタートを押すと指スマのシーン（`Yubisuma`）へ進む
/// ・**ビルドの一覧の先頭に入れる**（ビルドした .exe がタイトルから始まるように）
///
/// ※ Editor フォルダにあるため、ゲームのビルドには含まれない。
/// </summary>
public static class TitleSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Title.unity";

    /// <summary>タイトル画面の背景の絵</summary>
    private const string BackgroundPath = "Assets/Sprites/title.png";

    private static readonly Color BackgroundColor = new Color(0.12f, 0.14f, 0.20f);

    [MenuItem("Tools/Gamejam04/タイトル画面のシーンを作る")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog(
                "タイトル画面",
                $"{ScenePath} はすでにあります。作り直しますか？\n（手で調整した内容は消えます）",
                "作り直す", "やめる"))
        {
            return;
        }

        Create();
    }

    /// <summary>
    /// 確認を出さずに作り直す（コマンドから呼ぶ入口）。
    ///   -executeMethod TitleSceneSetup.CreateSceneFromCommandLine
    /// </summary>
    public static void CreateSceneFromCommandLine()
    {
        Create();
    }

    private static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundColor;
        cameraObject.AddComponent<AudioListener>();

        // ボタンを押すための係。このプロジェクトは新しい入力の仕組み（Input System）なので、そちら用の部品を付ける
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();

        // 画面いっぱいのUI。1920×1080 を基準に、窓の大きさに合わせて拡大縮小する
        GameObject canvasObject = new GameObject("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 背景の絵。いちばん奥（最初の子）に置く
        bool hasBackground = CreateBackground(canvasObject.transform);

        Text title = CreateText(canvasObject.transform, "Title", "指スマ", font, 160, Color.white);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchoredPosition = new Vector2(0f, 180f);
        titleRect.sizeDelta = new Vector2(1200f, 240f);
        title.fontStyle = FontStyle.Bold;

        // 背景の絵にタイトルの文字が入っているので、重ならないよう文字は隠しておく（消してはいない）
        title.gameObject.SetActive(!hasBackground);

        Button startButton = CreateButton(canvasObject.transform, "StartButton", "スタート", font);
        RectTransform buttonRect = (RectTransform)startButton.transform;

        // 背景があるときは、絵の真ん中（指先が触れ合うところ）を隠さないよう下のほうに置く
        buttonRect.anchoredPosition = new Vector2(0f, hasBackground ? -460f : -160f);
        buttonRect.sizeDelta = hasBackground ? new Vector2(420f, 100f) : new Vector2(480f, 120f);

        TitleMenu menu = canvasObject.AddComponent<TitleMenu>();
        SerializedObject serialized = new SerializedObject(menu);
        serialized.FindProperty("startButton").objectReferenceValue = startButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutFirstInBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log("タイトル画面のシーンを作りました。\nシーン: " + ScenePath + "（ビルドの一覧の先頭に入れました）");
    }

    /// <summary>
    /// 背景の絵を画面いっぱいに敷く。絵が見つからなければ何もしない（false を返す）。
    ///
    /// 絵（3:2）と画面（16:9）の形が違うので、**すき間が出ないよう大きめに敷き、はみ出た分は切る**。
    /// 絵の上のほうにタイトルの文字があるため、**上端をそろえて下側だけを切る**。
    /// </summary>
    private static bool CreateBackground(Transform canvas)
    {
        PrepareBackgroundImport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        if (texture == null)
        {
            Debug.LogWarning($"背景の絵 {BackgroundPath} が見つかりません。背景は無地のままにします。");
            return false;
        }

        GameObject background = new GameObject("Background", typeof(RectTransform));
        background.transform.SetParent(canvas, false);

        RawImage image = background.AddComponent<RawImage>();
        image.texture = texture;

        // ボタンを押すときの邪魔にならないよう、クリックを受け取らないようにする
        image.raycastTarget = false;

        // 上端をそろえる（はみ出た分は下に伸びる）
        RectTransform rect = (RectTransform)background.transform;
        rect.pivot = new Vector2(0.5f, 1f);

        AspectRatioFitter fitter = background.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = (float)texture.width / texture.height;

        return true;
    }

    /// <summary>
    /// 背景の絵を **UI 用（Sprite）** として読み込む設定にする。
    ///
    /// 初期設定（Default）のままだと、Unity が横幅 1536 を 2048 に**引き伸ばして**取り込むため、
    /// 絵が横に伸び、縦横の比率も狂う。UI 用にすると元の大きさのまま取り込まれる。
    /// </summary>
    private static void PrepareBackgroundImport()
    {
        TextureImporter importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
        if (importer == null || importer.textureType == TextureImporterType.Sprite)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static Text CreateText(Transform parent, string name, string content, Font font, int size, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Font font)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.white;

        Button button = buttonObject.AddComponent<Button>();

        // 選んでいる（キーボードで押せる）ときは黄色くして、どこが選ばれているか分かるようにする
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.90f, 0.90f, 0.90f);
        colors.highlightedColor = new Color(1f, 0.85f, 0.2f);
        colors.selectedColor = new Color(1f, 0.85f, 0.2f);
        colors.pressedColor = new Color(0.85f, 0.65f, 0.1f);
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, "Label", label, font, 64, new Color(0.12f, 0.14f, 0.20f));
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return button;
    }

    /// <summary>ビルドの一覧の先頭にタイトルを置く（すでにあれば先頭へ移す）。</summary>
    private static void PutFirstInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
