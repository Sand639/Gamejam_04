using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// **指スマの基礎シーン**を作るツール。
///
/// Unityのメニュー「Tools > Gamejam04 > 指スマの基礎シーンを作る」から実行できる。
///
/// ・画面を左右に2分割（左がプレイヤー1、右がプレイヤー2）
/// ・プレイヤーごとに両手を置く。手は `Assets/Prefab/LeftHand` ／ `RightHand` のプレハブ
/// ・キーは合計4つ
///     プレイヤー1 … 左手 A ／ 右手 D
///     プレイヤー2 … 左手 ← ／ 右手 →
/// ・押している間、親指が上がる
///
/// 2人の手は**離れた場所に置き、それぞれ専用のカメラで映す**。
/// カメラの Viewport Rect で、画面の左半分・右半分に割り当てている。
/// **カメラは手の後ろから映す**（自分の手を見ている向き。左に左手、右に右手が出る）。
///
/// ※ Editor フォルダにあるため、ゲームのビルドには含まれない。
/// </summary>
public static class YubisumaSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Yubisuma.unity";

    private const string LeftHandPrefabPath = "Assets/Prefab/LeftHand.prefab";
    private const string RightHandPrefabPath = "Assets/Prefab/RightHand.prefab";

    /// <summary>
    /// 指スマ用の手のアニメーション。**握りこぶし（Hand_Idle）だけ**を持つ。
    /// 元の HandController は Idle から中指のポーズへ自動で移るため、指スマでは使わない。
    /// </summary>
    private const string HandControllerPath = "Assets/Animations/Hand/YubisumaHand.controller";
    private const string IdleClipPath = "Assets/Animations/Hand/Hand_Idle.anim";

    /// <summary>2人の手をどれだけ離して置くか（互いのカメラに映り込まないように）</summary>
    private const float PlayerSpacing = 40f;

    /// <summary>左右の手の間の半分の距離（m）</summary>
    private const float HandHalfSpacing = 0.5f;

    /// <summary>カメラを手からどれだけ後ろ・上に置くか（m）と、見下ろす角度（度）</summary>
    private const float CameraDistance = 3.2f;
    private const float CameraHeight = 1.0f;
    private const float CameraPitch = 13f;

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

        Create();
    }

    /// <summary>
    /// 確認を出さずに作り直す（コマンドから呼ぶ入口）。
    ///   -executeMethod YubisumaSceneSetup.CreateSceneFromCommandLine
    /// </summary>
    public static void CreateSceneFromCommandLine()
    {
        Create();
    }

    private static void Create()
    {
        GameObject leftPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeftHandPrefabPath);
        GameObject rightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RightHandPrefabPath);

        if (leftPrefab == null || rightPrefab == null)
        {
            Debug.LogError($"手のプレハブが見つかりません。\n{LeftHandPrefabPath}\n{RightHandPrefabPath}");
            return;
        }

        RuntimeAnimatorController handController = EnsureHandController();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(40f, 160f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;

        YubisumaPlayer player1 = CreatePlayer(
            "Player1", "プレイヤー1", new Vector3(-PlayerSpacing * 0.5f, 0f, 0f),
            new Rect(0f, 0f, 0.5f, 1f), new Color(0.20f, 0.35f, 0.60f),
            Key.A, Key.D, leftPrefab, rightPrefab, handController, withAudioListener: true);

        YubisumaPlayer player2 = CreatePlayer(
            "Player2", "プレイヤー2", new Vector3(PlayerSpacing * 0.5f, 0f, 0f),
            new Rect(0.5f, 0f, 0.5f, 1f), new Color(0.60f, 0.22f, 0.22f),
            Key.LeftArrow, Key.RightArrow, leftPrefab, rightPrefab, handController, withAudioListener: false);

        GameObject hudObject = new GameObject("YubisumaHud");
        YubisumaHud hud = hudObject.AddComponent<YubisumaHud>();
        SerializedObject hudSerialized = new SerializedObject(hud);
        SerializedProperty playersProperty = hudSerialized.FindProperty("players");
        playersProperty.arraySize = 2;
        playersProperty.GetArrayElementAtIndex(0).objectReferenceValue = player1;
        playersProperty.GetArrayElementAtIndex(1).objectReferenceValue = player2;
        hudSerialized.ApplyModifiedPropertiesWithoutUndo();

        // 試合の流れ（指スマスタート → 数字で指定 → スペースでいっせーの → 判定 → 番の交代）
        GameObject matchObject = new GameObject("YubisumaMatch");
        YubisumaMatch match = matchObject.AddComponent<YubisumaMatch>();
        SerializedObject matchSerialized = new SerializedObject(match);
        SerializedProperty matchPlayers = matchSerialized.FindProperty("players");
        matchPlayers.arraySize = 2;
        matchPlayers.GetArrayElementAtIndex(0).objectReferenceValue = player1;
        matchPlayers.GetArrayElementAtIndex(1).objectReferenceValue = player2;
        matchSerialized.ApplyModifiedPropertiesWithoutUndo();

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
        Key leftKey, Key rightKey, GameObject leftPrefab, GameObject rightPrefab,
        RuntimeAnimatorController handController, bool withAudioListener)
    {
        GameObject root = new GameObject(objectName);
        root.transform.position = position;

        // 自分専用のカメラ。Viewport Rect で画面の左右どちらに出すかを決める。
        // 手の後ろ（+Z 側）から、手のほう（-Z 方向）を見る
        GameObject cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, CameraHeight, CameraDistance);
        cameraObject.transform.localRotation = Quaternion.Euler(CameraPitch, 180f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.rect = viewport;
        camera.fieldOfView = 40f;
        camera.nearClipPlane = 0.05f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background;

        if (withAudioListener)
        {
            // 音を聞く係は1つだけにする（2つあると警告が出る）
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
        }

        // -Z 方向を見ているので、+X が画面の左になる。左手を +X、右手を -X に置く
        YubisumaThumb leftThumb = CreateHand(root.transform, leftPrefab, HandHalfSpacing, leftKey, handController);
        YubisumaThumb rightThumb = CreateHand(root.transform, rightPrefab, -HandHalfSpacing, rightKey, handController);

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
    /// 手のプレハブを1つ置き、親指を動かす部品を付ける。
    /// プレハブとのつながりは残す（プレハブを直せば、このシーンの手にも反映される）。
    /// </summary>
    private static YubisumaThumb CreateHand(
        Transform parent, GameObject prefab, float x, Key key, RuntimeAnimatorController handController)
    {
        GameObject hand = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        hand.transform.localPosition = new Vector3(x, 0f, 0f);

        // 握りこぶしのままにする（元のコントローラーは中指のポーズへ移ってしまう）
        Animator animator = hand.GetComponentInChildren<Animator>();
        if (animator != null && handController != null)
        {
            animator.runtimeAnimatorController = handController;
        }

        Transform thumbRoot = FindDeep(hand.transform, "Thumb");
        Transform thumbMiddle = FindDeep(hand.transform, "Thumb2");
        Transform thumbTip = thumbMiddle != null && thumbMiddle.childCount > 0 ? thumbMiddle.GetChild(0) : null;

        if (thumbRoot == null || thumbTip == null)
        {
            Debug.LogError($"{hand.name} に親指の骨（Thumb／Thumb2）が見つかりません。");
        }

        YubisumaThumb thumb = hand.AddComponent<YubisumaThumb>();
        SerializedObject serialized = new SerializedObject(thumb);
        serialized.FindProperty("key").intValue = (int)key;
        serialized.FindProperty("thumbRoot").objectReferenceValue = thumbRoot;
        serialized.FindProperty("thumbMiddle").objectReferenceValue = thumbMiddle;
        serialized.FindProperty("thumbTip").objectReferenceValue = thumbTip;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return thumb;
    }

    // ------------------------------------------------------------
    // 補助
    // ------------------------------------------------------------

    /// <summary>握りこぶし（Hand_Idle）だけのアニメーションを用意する。すでにあればそれを使う。</summary>
    private static RuntimeAnimatorController EnsureHandController()
    {
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(HandControllerPath);
        if (existing != null)
        {
            return existing;
        }

        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        if (idle == null)
        {
            Debug.LogWarning($"{IdleClipPath} が見つかりません。手はプレハブのアニメーションのまま動きます。");
            return null;
        }

        return AnimatorController.CreateAnimatorControllerAtPathWithClip(HandControllerPath, idle);
    }

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
}
