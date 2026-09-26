using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// **指スマの画面の UI（YubisumaUI）を付けるツール。**
///
/// Unityのメニュー「Tools > Gamejam04 > 指スマの画面のUIを付ける」から実行できる。
///
/// **シーンを作り直さずに、今のシーンに付け足す**（手の位置などの調整は消えない）。
/// すでに付いていれば、画像の割り当てだけやり直す。
///
///   ・キーの画像 … Assets/Sprites/Key（A・D・ArrowL・ArrowR・W・ArrowUp）
///   ・丸 … Assets/Sprites/UI（WinEmpty・WinFilled）※仮の素材
///   ・吹き出し … Assets/Sprites/UI/SpeechBubble（チームで用意したもの）
///
/// ※ Editor フォルダにあるため、ゲームのビルドには含まれない。
/// </summary>
public static class YubisumaUISetup
{
    private const string ScenePath = "Assets/Scenes/Yubisuma.unity";
    private const string KeyFolder = "Assets/Sprites/Key";
    private const string UiFolder = "Assets/Sprites/UI";

    private static readonly Dictionary<Key, string> KeyFiles = new Dictionary<Key, string>
    {
        { Key.A, "A" },
        { Key.D, "D" },
        { Key.LeftArrow, "ArrowL" },
        { Key.RightArrow, "ArrowR" },
        { Key.W, "W" },
        { Key.UpArrow, "ArrowUp" },
    };

    [MenuItem("Tools/Gamejam04/指スマの画面のUIを付ける")]
    public static void AttachToScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath);
        YubisumaMatch match = Object.FindFirstObjectByType<YubisumaMatch>(FindObjectsInactive.Include);

        if (match == null)
        {
            Debug.LogError($"{ScenePath} に YubisumaMatch が見つかりません。");
            return;
        }

        Attach(match);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("指スマの画面のUIを付けました。");
    }

    /// <summary>今開いているシーンに UI を付ける（すでにあれば画像の割り当てだけやり直す）。</summary>
    public static void Attach(YubisumaMatch match)
    {
        PrepareSprites();

        YubisumaUI ui = Object.FindFirstObjectByType<YubisumaUI>(FindObjectsInactive.Include);

        if (ui == null)
        {
            ui = new GameObject("YubisumaUI").AddComponent<YubisumaUI>();
        }

        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("match").objectReferenceValue = match;
        serialized.FindProperty("winEmpty").objectReferenceValue = LoadSprite($"{UiFolder}/WinEmpty.png");
        serialized.FindProperty("winFilled").objectReferenceValue = LoadSprite($"{UiFolder}/WinFilled.png");
        serialized.FindProperty("burst").objectReferenceValue = LoadSprite($"{UiFolder}/SpeechBubble.png");

        SerializedProperty keys = serialized.FindProperty("keySprites");
        keys.arraySize = KeyFiles.Count;
        int index = 0;
        foreach (KeyValuePair<Key, string> pair in KeyFiles)
        {
            SerializedProperty entry = keys.GetArrayElementAtIndex(index++);
            entry.FindPropertyRelative("key").intValue = (int)pair.Key;
            entry.FindPropertyRelative("sprite").objectReferenceValue = LoadSprite($"{KeyFolder}/{pair.Value}.png");
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>丸と吹き出しの画像を UI 用（Sprite）として読み込む設定にする。</summary>
    private static void PrepareSprites()
    {
        foreach (string name in new[] { "WinEmpty", "WinFilled", "SpeechBubble" })
        {
            string path = $"{UiFolder}/{name}.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null || importer.textureType == TextureImporterType.Sprite)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
        {
            Debug.LogWarning($"画像 {path} が見つかりません（または Sprite になっていません）。");
        }

        return sprite;
    }
}
