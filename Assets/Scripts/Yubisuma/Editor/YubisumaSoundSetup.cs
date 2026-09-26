using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// **指スマの効果音を割り当てるツール。**
///
/// Unityのメニュー「Tools > Gamejam04 > 指スマの効果音を割り当てる」から実行できる。
///
/// いまは、**当てられた手が画面の外へ流れていくときの音**（`Assets/Audio/f01.wav`・`f02.wav`）を
/// `YubisumaMatch` の Hand Out Sounds に入れる。
///
/// **シーンを作り直さずに、今のシーンに割り当てる**（手の位置などの調整は消えない）。
///
/// ※ Editor フォルダにあるため、ゲームのビルドには含まれない。
/// </summary>
public static class YubisumaSoundSetup
{
    private const string ScenePath = "Assets/Scenes/Yubisuma.unity";

    /// <summary>手が流れていくときの音。ここに並べたものからランダムで鳴る</summary>
    private static readonly string[] HandOutSoundPaths =
    {
        "Assets/Audio/f01.wav",
        "Assets/Audio/f02.wav",
    };

    [MenuItem("Tools/Gamejam04/指スマの効果音を割り当てる")]
    public static void AssignToScene()
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

        SerializedObject serialized = new SerializedObject(match);
        Assign(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("指スマの効果音を割り当てました（手が流れていくときの音）。");
    }

    /// <summary>
    /// <see cref="YubisumaMatch"/> の効果音の項目に音声を入れる。
    /// 呼んだ側で ApplyModifiedPropertiesWithoutUndo すること。
    /// </summary>
    public static void Assign(SerializedObject match)
    {
        SerializedProperty sounds = match.FindProperty("handOutSounds");
        sounds.arraySize = HandOutSoundPaths.Length;

        for (int i = 0; i < HandOutSoundPaths.Length; i++)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(HandOutSoundPaths[i]);

            if (clip == null)
            {
                Debug.LogWarning($"音声 {HandOutSoundPaths[i]} が見つかりません。この音は鳴りません。");
            }

            sounds.GetArrayElementAtIndex(i).objectReferenceValue = clip;
        }
    }
}
