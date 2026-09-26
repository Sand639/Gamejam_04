using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// **指スマの基礎シーン**を作るツール。
///
/// Unityのメニュー「Tools > Gamejam04 > 指スマの基礎シーンを作る」から実行できる。
///
/// ・画面を左右に2分割（左がプレイヤー1、右がプレイヤー2）
/// ・プレイヤーごとに両手の親指を置く。キーは合計4つ
///     プレイヤー1 … 左手 A ／ 右手 D
///     プレイヤー2 … 左手 ← ／ 右手 →
/// ・押している間、親指が上がる
///
/// 2人の手は**離れた場所に置き、それぞれ専用のカメラで映す**。
/// カメラの Viewport Rect で、画面の左半分・右半分に割り当てている。
///
/// ※ Editor フォルダにあるため、ゲームのビルドには含まれない。
/// </summary>
public static class YubisumaSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Yubisuma.unity";
    private const string MaterialFolder = "Assets/Materials/Yubisuma";

    /// <summary>2人の手をどれだけ離して置くか（互いのカメラに映り込まないように）</summary>
    private const float PlayerSpacing = 40f;

    private static readonly Color SkinColor = new Color(0.95f, 0.78f, 0.62f);

    [MenuItem("Tools/Gamejam04/指スマの基礎シーンを作る")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog(
                "指スマの基礎シーン",
                $"{ScenePath} はすでにあります。作り直しますか？\n（手で調整した内容は消えます）",
                "作り直す", "やめる"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureFolder("Assets/Materials");
        EnsureFolder(MaterialFolder);
        Material skin = CreateMaterial(MaterialFolder + "/YubisumaSkin.mat", SkinColor);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(40f, -20f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;

        YubisumaPlayer player1 = CreatePlayer(
            "Player1", "プレイヤー1", new Vector3(-PlayerSpacing * 0.5f, 0f, 0f),
            new Rect(0f, 0f, 0.5f, 1f), new Color(0.20f, 0.35f, 0.60f),
            Key.A, Key.D, skin, withAudioListener: true);

        YubisumaPlayer player2 = CreatePlayer(
            "Player2", "プレイヤー2", new Vector3(PlayerSpacing * 0.5f, 0f, 0f),
            new Rect(0.5f, 0f, 0.5f, 1f), new Color(0.60f, 0.22f, 0.22f),
            Key.LeftArrow, Key.RightArrow, skin, withAudioListener: false);

        GameObject hudObject = new GameObject("YubisumaHud");
        YubisumaHud hud = hudObject.AddComponent<YubisumaHud>();
        SerializedObject hudSerialized = new SerializedObject(hud);
        SerializedProperty playersProperty = hudSerialized.FindProperty("players");
        playersProperty.arraySize = 2;
        playersProperty.GetArrayElementAtIndex(0).objectReferenceValue = player1;
        playersProperty.GetArrayElementAtIndex(1).objectReferenceValue = player2;
        hudSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log("指スマの基礎シーンを作りました。\nシーン: " + ScenePath +
                  "\nプレイヤー1：A（左手）／D（右手）　プレイヤー2：←（左手）／→（右手）");
    }

    // ------------------------------------------------------------
    // プレイヤー1人分
    // ------------------------------------------------------------

    private static YubisumaPlayer CreatePlayer(
        string objectName, string displayName, Vector3 position, Rect viewport, Color background,
        Key leftKey, Key rightKey, Material skin, bool withAudioListener)
    {
        GameObject root = new GameObject(objectName);
        root.transform.position = position;

        // 自分専用のカメラ。Viewport Rect で画面の左右どちらに出すかを決める
        GameObject cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.2f, -5f);
        cameraObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.rect = viewport;
        camera.fieldOfView = 45f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background;

        if (withAudioListener)
        {
            // 音を聞く係は1つだけにする（2つあると警告が出る）
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
        }

        // 左手は内側（右）へ、右手は内側（左）へ親指を倒す
        YubisumaThumb leftThumb = CreateHand(root.transform, "LeftHand", -1.1f, leftKey, -80f, skin);
        YubisumaThumb rightThumb = CreateHand(root.transform, "RightHand", 1.1f, rightKey, 80f, skin);

        YubisumaPlayer player = root.AddComponent<YubisumaPlayer>();
        SerializedObject serialized = new SerializedObject(player);
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("playerCamera").objectReferenceValue = camera;
        SerializedProperty thumbs = serialized.FindProperty("thumbs");
        thumbs.arraySize = 2;
        thumbs.GetArrayElementAtIndex(0).objectReferenceValue = leftThumb;
        thumbs.GetArrayElementAtIndex(1).objectReferenceValue = rightThumb;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    /// <summary>
    /// 手を1つ作る。握りこぶし（箱）の上に、親指の付け根を置く。
    /// **付け根を回して親指を上げ下げする**ので、見た目は付け根の子にする。
    /// </summary>
    private static YubisumaThumb CreateHand(
        Transform parent, string name, float x, Key key, float loweredAngle, Material skin)
    {
        GameObject hand = new GameObject(name);
        hand.transform.SetParent(parent, false);
        hand.transform.localPosition = new Vector3(x, 0f, 0f);

        GameObject fist = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fist.name = "Fist";
        fist.transform.SetParent(hand.transform, false);
        fist.transform.localScale = new Vector3(1.2f, 1f, 1f);
        fist.GetComponent<MeshRenderer>().sharedMaterial = skin;
        Object.DestroyImmediate(fist.GetComponent<Collider>());

        GameObject pivot = new GameObject("ThumbPivot");
        pivot.transform.SetParent(hand.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 0.5f, -0.15f);

        GameObject thumbVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        thumbVisual.name = "Thumb";
        thumbVisual.transform.SetParent(pivot.transform, false);
        thumbVisual.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        thumbVisual.transform.localScale = new Vector3(0.32f, 0.9f, 0.36f);
        MeshRenderer thumbRenderer = thumbVisual.GetComponent<MeshRenderer>();
        thumbRenderer.sharedMaterial = skin;
        Object.DestroyImmediate(thumbVisual.GetComponent<Collider>());

        YubisumaThumb thumb = pivot.AddComponent<YubisumaThumb>();
        SerializedObject serialized = new SerializedObject(thumb);
        serialized.FindProperty("key").intValue = (int)key;
        serialized.FindProperty("loweredAngle").floatValue = loweredAngle;
        serialized.FindProperty("thumbRenderer").objectReferenceValue = thumbRenderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 最初から下げた形で置いておく（再生前のシーン画面でも分かりやすいように）
        pivot.transform.localRotation = Quaternion.Euler(0f, 0f, loweredAngle);

        return thumb;
    }

    // ------------------------------------------------------------
    // 補助
    // ------------------------------------------------------------

    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        foreach (EditorBuildSettingsScene existing in scenes)
        {
            if (existing.path == ScenePath)
            {
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static Material CreateMaterial(string path, Color color)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int lastSlash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, lastSlash), path.Substring(lastSlash + 1));
    }
}
