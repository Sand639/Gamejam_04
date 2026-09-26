using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// **接続の設定画面だけを載せたシーン**を作るツール。
///
/// Unityのメニュー「Tools > Gamejam04 > オンライン接続シーンを作る」から実行できる。
///
/// シーンには `NetworkManager` と、接続画面・合言葉でつなぐ部品だけを置く。
/// **プレイヤーやゲームの中身は入れていない**（Mirai01 から移植したのは設定画面だけ）。
///
/// ※ Editor フォルダにあるため、ゲームのビルドには含まれない。
/// </summary>
public static class NetworkConnectSceneSetup
{
    private const string ScenePath = "Assets/Scenes/NetworkConnect.unity";

    /// <summary>1秒あたり何回、位置などを送るか。初期値は30。増やすと反応が良くなる</summary>
    private const uint NetworkTickRate = 60;

    [MenuItem("Tools/Gamejam04/オンライン接続シーンを作る")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog(
                "オンライン接続シーン",
                $"{ScenePath} はすでにあります。作り直しますか？\n（手で調整した内容は消えます）",
                "作り直す", "やめる"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 1f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
        cameraObject.AddComponent<AudioListener>();

        // 通信のまとめ役
        GameObject managerObject = new GameObject("NetworkManager");
        NetworkManager manager = managerObject.AddComponent<NetworkManager>();
        UnityTransport transport = managerObject.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 7777);

        manager.NetworkConfig.NetworkTransport = transport;

        // 送る回数を増やして、反応を良くする（初期値は30）
        manager.NetworkConfig.TickRate = NetworkTickRate;

        managerObject.AddComponent<LanConnectionUi>();
        managerObject.AddComponent<InternetConnection>();
        managerObject.AddComponent<LanAutoStart>();
        managerObject.AddComponent<LanConnectionLogger>();
        managerObject.AddComponent<NetworkStatusHud>();

        EditorUtility.SetDirty(manager);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();

        AssetDatabase.SaveAssets();
        Debug.Log("オンライン接続シーンを作りました。\nシーン: " + ScenePath);
    }

    /// <summary>ビルドの一覧に無ければ足す（ビルドした .exe で試せるように）。</summary>
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
