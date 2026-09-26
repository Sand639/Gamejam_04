using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// **指スマの声と効果音を割り当てるツール。**
///
/// Unityのメニュー「Tools > Gamejam04 > 指スマの効果音を割り当てる」から実行できる。
///
/// `Assets/Audio/NEWSE/` の音声を、`YubisumaMatch` の各項目に入れる。
///
///   ・「いっせーの」の声 … いっせーの改善.wav
///   ・数字の声（0～4）  … 0.wav ～ 4.wav
///   ・スキルの声         … コンクリート.wav ほか
///   ・手が流れていく音   … f.wav
///
/// **シーンを作り直さずに、今のシーンに割り当てる**（手の位置などの調整は消えない）。
///
/// ※ SE フォルダの .m4a は Unity で音声として読み込めないので、同じ名前の .wav に変換したもの
///   （前後の無音も切ったもの）を使う。
///
/// ※ Editor フォルダにあるため、ゲームのビルドには含まれない。
/// </summary>
public static class YubisumaSoundSetup
{
    private const string ScenePath = "Assets/Scenes/Yubisuma.unity";
    private const string SeFolder = "Assets/Audio/NEWSE";

    private const string IsseenoVoicePath = SeFolder + "/いっせーの改善.wav";

    /// <summary>手が流れていくときの音。ここに並べたものからランダムで鳴る</summary>
    private static readonly string[] HandOutSoundPaths =
    {
        SeFolder + "/f.wav",
    };

    /// <summary>
    /// スキルの声。<see cref="YubisumaSkillType"/> の並び（None, コンクリ, セメント, イーブン, オッズ, ピース, サンダー）と同じ順。
    /// </summary>
    private static readonly string[] SkillVoicePaths =
    {
        null,
        SeFolder + "/コンクリート.wav",
        SeFolder + "/セメント.wav",
        SeFolder + "/イーブン.wav",
        SeFolder + "/オッズ.wav",
        SeFolder + "/ピース.wav",
        SeFolder + "/サンダー.wav",
    };

    private const int NumberVoiceCount = 5;

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

        Debug.Log("指スマの声と効果音を割り当てました（いっせーの・数字・スキル・手が流れていく音）。");
    }

    /// <summary>
    /// <see cref="YubisumaMatch"/> の声と効果音の項目に音声を入れる。
    /// 呼んだ側で ApplyModifiedPropertiesWithoutUndo すること。
    /// </summary>
    public static void Assign(SerializedObject match)
    {
        match.FindProperty("isseenoVoice").objectReferenceValue = Load(IsseenoVoicePath);

        SerializedProperty numbers = match.FindProperty("numberVoices");
        numbers.arraySize = NumberVoiceCount;
        for (int i = 0; i < NumberVoiceCount; i++)
        {
            numbers.GetArrayElementAtIndex(i).objectReferenceValue = Load($"{SeFolder}/{i}.wav");
        }

        SerializedProperty skills = match.FindProperty("skillVoices");
        skills.arraySize = SkillVoicePaths.Length;
        for (int i = 0; i < SkillVoicePaths.Length; i++)
        {
            skills.GetArrayElementAtIndex(i).objectReferenceValue =
                SkillVoicePaths[i] == null ? null : Load(SkillVoicePaths[i]);
        }

        SerializedProperty sounds = match.FindProperty("handOutSounds");
        sounds.arraySize = HandOutSoundPaths.Length;
        for (int i = 0; i < HandOutSoundPaths.Length; i++)
        {
            sounds.GetArrayElementAtIndex(i).objectReferenceValue = Load(HandOutSoundPaths[i]);
        }
    }

    private static AudioClip Load(string path)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        if (clip == null)
        {
            Debug.LogWarning($"音声 {path} が見つかりません。この音は鳴りません。");
        }

        return clip;
    }
}
