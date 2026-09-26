using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// **ゲームを Windows 用にビルドするツール。**
///
/// Unityのメニューから実行できる。
///
///   ・Tools > Gamejam04 > ゲームをビルドする（Windows）          … 配る用
///   ・Tools > Gamejam04 > ゲームをビルドする（Windows・開発用） … ログが詳しく出る。不具合を調べるとき用
///
/// 入るシーンは **タイトル → 指スマ** の2つ（Build Profiles の一覧とは関係なく、この順で入れる）。
/// 出力先は `Build/Gamejam04/`（開発用は `Build/Gamejam04_Dev/`）。**Gitには入らない**。
/// 終わったらフォルダを開く。
///
/// ビルドの前に、指スマのシーンの **Test Skill が None 以外になっていたら知らせる**
/// （テスト用の設定のまま配ってしまわないように）。
///
/// コマンドから動かすときは：
///   -executeMethod GameBuilder.BuildFromCommandLine [-buildOutput ＜出力先＞] [-development]
///
/// ※ Editor フォルダにあるため、ゲームのビルドには含まれない。
/// </summary>
public static class GameBuilder
{
    /// <summary>ビルドに入れるシーン（最初のものから始まる）。</summary>
    private static readonly string[] Scenes =
    {
        "Assets/Scenes/Title.unity",
        "Assets/Scenes/Yubisuma.unity",
    };

    private const string YubisumaScenePath = "Assets/Scenes/Yubisuma.unity";
    private const string OutputFolder = "Build/Gamejam04";
    private const string DevOutputFolder = "Build/Gamejam04_Dev";

    [MenuItem("Tools/Gamejam04/ゲームをビルドする（Windows）")]
    public static void BuildFromMenu()
    {
        Build(OutputFolder, false, true);
    }

    [MenuItem("Tools/Gamejam04/ゲームをビルドする（Windows・開発用）")]
    public static void BuildDevelopmentFromMenu()
    {
        Build(DevOutputFolder, true, true);
    }

    /// <summary>コマンドから呼ぶ入口。</summary>
    public static void BuildFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool development = Array.IndexOf(args, "-development") >= 0;
        string output = development ? DevOutputFolder : OutputFolder;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-buildOutput")
            {
                output = args[i + 1];
            }
        }

        bool ok = Build(output, development, false);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Build(string outputFolder, bool development, bool interactive)
    {
        foreach (string scene in Scenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
            {
                Report(interactive, $"シーン {scene} が見つかりません。ビルドを中止しました。", true);
                return false;
            }
        }

        // テスト用の設定のまま配ってしまわないように
        string testSkill = ReadTestSkill();
        if (testSkill != null)
        {
            string message = $"指スマのシーンの Test Skill が「{testSkill}」になっています。\n" +
                             "このままだと、配られるスキルが全部それになります（ランダムになりません）。";

            if (interactive)
            {
                if (!EditorUtility.DisplayDialog("ビルド", message + "\n\nこのままビルドしますか？", "ビルドする", "やめる"))
                {
                    return false;
                }
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

        if (interactive && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return false;
        }

        string exeName = $"{PlayerSettings.productName}.exe";
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = Path.Combine(outputFolder, exeName),
            target = BuildTarget.StandaloneWindows64,
            options = development ? BuildOptions.Development : BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            Report(interactive, $"ビルドに失敗しました（{summary.result}）。Console のエラーを見てください。", true);
            return false;
        }

        string full = Path.GetFullPath(options.locationPathName);
        Debug.Log($"ビルドができました（{summary.totalTime.TotalSeconds:F0}秒・{summary.totalSize / (1024f * 1024f):F0}MB）。\n{full}");

        if (interactive)
        {
            EditorUtility.RevealInFinder(full);
        }

        return true;
    }

    /// <summary>
    /// 指スマのシーンの Test Skill を読む。None（0）なら null、それ以外ならスキルの名前。
    /// シーンを開かずに、ファイルの中身から読む（いま開いているシーンを切り替えないため）。
    /// </summary>
    private static string ReadTestSkill()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), YubisumaScenePath);
        if (!File.Exists(path))
        {
            return null;
        }

        Match match = Regex.Match(File.ReadAllText(path), @"^\s*testSkill:\s*(\d+)", RegexOptions.Multiline);
        if (!match.Success || match.Groups[1].Value == "0")
        {
            return null;
        }

        int value = int.Parse(match.Groups[1].Value);
        return Enum.IsDefined(typeof(YubisumaSkillType), value)
            ? YubisumaSkill.NameOf((YubisumaSkillType)value)
            : match.Groups[1].Value;
    }

    private static void Report(bool interactive, string message, bool error)
    {
        if (error)
        {
            Debug.LogError(message);
        }
        else
        {
            Debug.Log(message);
        }

        if (interactive)
        {
            EditorUtility.DisplayDialog("ビルド", message, "OK");
        }
    }
}
