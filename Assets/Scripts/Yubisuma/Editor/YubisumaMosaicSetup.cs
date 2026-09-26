using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// **指スマの手に、中指のモザイクを付けるツール。**
///
/// Unityのメニュー「Tools > Gamejam04 > 指スマの手に中指のモザイクを付ける」から実行できる。
///
/// **シーンを作り直さずに、今のシーンに付け足す**（手の位置などの調整は消えない）。
/// すでに付いている手には何もしないので、何度押してもよい。
///
/// ※ Editor フォルダにあるため、ゲームのビルドには含まれない。
/// </summary>
public static class YubisumaMosaicSetup
{
    private const string ScenePath = "Assets/Scenes/Yubisuma.unity";
    private const string MosaicPrefabPath = "Assets/takuma/prefabs/mosaic.prefab";

    [MenuItem("Tools/Gamejam04/指スマの手に中指のモザイクを付ける")]
    public static void AttachToScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath);
        int added = 0;

        foreach (YubisumaPlayer player in Object.FindObjectsByType<YubisumaPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (YubisumaThumb hand in player.Thumbs)
            {
                if (hand != null && Attach(hand, player.transform, player.PlayerCamera))
                {
                    added++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"指スマの手に中指のモザイクを付けました（{added}本の手）。すでに付いていた手はそのままです。");
    }

    /// <summary>
    /// 手1つにモザイクを付ける。すでに付いていれば何もしない（false を返す）。
    /// モザイクの板は <paramref name="container"/>（プレイヤー）の下に置く。
    /// </summary>
    public static bool Attach(YubisumaThumb hand, Transform container, Camera viewCamera)
    {
        if (hand.GetComponent<YubisumaFingerMosaic>() != null)
        {
            return false;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MosaicPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"モザイクのプレハブ {MosaicPrefabPath} が見つかりません。");
            return false;
        }

        Transform fingerRoot = FindDeep(hand.transform, "MiddleFinger");
        Transform middle3 = FindDeep(hand.transform, "Middle3");
        Transform fingerTip = middle3 != null && middle3.childCount > 0 ? middle3.GetChild(0) : middle3;

        if (fingerRoot == null || fingerTip == null)
        {
            Debug.LogError($"{hand.name} に中指の骨（MiddleFinger／Middle3）が見つかりません。");
            return false;
        }

        // 右手のプレハブは左右反転しているので、板は手の子にせず、プレイヤーの下に置く
        GameObject mosaic = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
        mosaic.name = $"Mosaic_{hand.name}";
        mosaic.transform.localPosition = hand.transform.localPosition;
        mosaic.transform.localRotation = Quaternion.identity;

        // 当たり判定は要らない（あると手を流すときなどに邪魔になりうる）
        Collider collider = mosaic.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider, true);
        }

        YubisumaFingerMosaic component = hand.gameObject.AddComponent<YubisumaFingerMosaic>();
        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty("hand").objectReferenceValue = hand;
        serialized.FindProperty("fingerRoot").objectReferenceValue = fingerRoot;
        serialized.FindProperty("fingerTip").objectReferenceValue = fingerTip;
        serialized.FindProperty("mosaic").objectReferenceValue = mosaic.transform;
        serialized.FindProperty("viewCamera").objectReferenceValue = viewCamera;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return true;
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
}
