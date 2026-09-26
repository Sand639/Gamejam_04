using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// **接続を確かめるための、Windows用ビルドを作るツール。**
///
/// 通信は「2つ以上のゲームを同時に動かす」必要があるため、
/// エディタの再生ボタンだけでは確かめきれない。
/// このツールで .exe を作り、一緒に作られる `.bat` で複数起動して試す。
///
/// Unityのメニュー「Tools > Gamejam04 > 接続確認用のビルドを作る」から実行できる。
/// 出力先は `Build/NetworkTest/`（Gitには入らない場所）。
///
/// 接続シーン（`NetworkConnect.unity`）が無ければ、先に自動で作る。
///
/// コマンドから動かすときは、引数で出力先を変えられる。
///   -executeMethod NetworkTestBuilder.BuildFromCommandLine -buildOutput ＜出力先＞
/// </summary>
public static class NetworkTestBuilder
{
    private const string DefaultOutputFolder = "Build/NetworkTest";
    private const string ExeName = "NetworkTest.exe";

    // 並べて見られるように、全画面ではなく小さめの窓で起動させる
    private const string WindowArgs = "-screen-fullscreen 0 -screen-width 960 -screen-height 540";

    [MenuItem("Tools/Gamejam04/接続確認用のビルドを作る")]
    public static void BuildFromMenu()
    {
        Build(DefaultOutputFolder);
    }

    /// <summary>コマンドから呼ぶ入口。`-buildOutput ＜出力先＞` で場所を指定できる。</summary>
    public static void BuildFromCommandLine()
    {
        string output = DefaultOutputFolder;
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-buildOutput")
            {
                output = args[i + 1];
                break;
            }
        }

        Build(output);
    }

    private static void Build(string outputFolder)
    {
        if (!NetworkConnectSceneSetup.EnsureScene())
        {
            Debug.LogError("接続シーンが無いため、ビルドを中止しました。");
            return;
        }

        if (string.IsNullOrEmpty(Application.cloudProjectId))
        {
            // LANは動くので止めない。インターネットで試す人に先に気づいてもらう
            Debug.LogWarning(
                "Unity Cloud への登録が済んでいません。LANの接続は確かめられますが、" +
                "インターネット（合言葉）では部屋を作れません。\n" +
                "手順：Documents/インターネットでの複数人プレイ.md");
        }

        // 窓の端をドラッグして大きさを変えられるようにする。
        // 初期設定ではOFFで、複数並べたときに大きさを変えられなかったため。
        // （Player Settings に残るので、ふつうのビルドでも変えられるようになる）
        if (!PlayerSettings.resizableWindow)
        {
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("Player Settings の Resizable Window を ON にしました（窓の大きさを変えられるようにするため）。");
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { NetworkConnectSceneSetup.ScenePath },
            locationPathName = Path.Combine(outputFolder, ExeName),
            target = BuildTarget.StandaloneWindows64,

            // 開発用ビルド。ログが詳しく出るので、つながらないときに原因を追える
            options = BuildOptions.Development,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"ビルドに失敗しました：{report.summary.result}");
            return;
        }

        CreateLaunchScripts(outputFolder);

        string full = Path.GetFullPath(outputFolder);
        Debug.Log($"接続確認用のビルドができました。\n{full}\n" +
                  "LAN：「LAN_2人で自動接続.bat」／インターネット：「ネット_部屋を作る.bat」→「ネット_合言葉で参加.bat」");

        // メニューから実行したときは、できたフォルダをそのまま開く
        if (!Application.isBatchMode)
        {
            EditorUtility.RevealInFinder(options.locationPathName);
        }
    }

    /// <summary>
    /// 複数起動するための `.bat` を、ビルドの隣に置く。
    ///
    /// **.bat の中身は半角英数字だけにする**（日本語を書くと文字化けするため）。
    /// 同じPCで複数起動しても、ゲーム側が起動順で自動的に別人として扱うので、
    /// ここでは何も指定しなくてよい。
    /// </summary>
    private static void CreateLaunchScripts(string outputFolder)
    {
        string exe = $"\"%~dp0{ExeName}\"";

        // LAN：起動しただけでホストと参加者がつながる（動作確認はこれが一番速い）
        Write(outputFolder, "LAN_2人で自動接続.bat",
            "@echo off\r\n" +
            "rem LAN: start HOST + 1 CLIENT on this PC and connect them\r\n" +
            $"start \"HOST\" {exe} -host {WindowArgs}\r\n" +
            "timeout /t 3 /nobreak >nul\r\n" +
            $"start \"CLIENT1\" {exe} -client 127.0.0.1 {WindowArgs}\r\n");

        // LAN：4人分
        Write(outputFolder, "LAN_4人で自動接続.bat",
            "@echo off\r\n" +
            "rem LAN: start HOST + 3 CLIENTS on this PC and connect them\r\n" +
            $"start \"HOST\" {exe} -host {WindowArgs}\r\n" +
            "timeout /t 3 /nobreak >nul\r\n" +
            $"start \"CLIENT1\" {exe} -client 127.0.0.1 {WindowArgs}\r\n" +
            "timeout /t 1 /nobreak >nul\r\n" +
            $"start \"CLIENT2\" {exe} -client 127.0.0.1 {WindowArgs}\r\n" +
            "timeout /t 1 /nobreak >nul\r\n" +
            $"start \"CLIENT3\" {exe} -client 127.0.0.1 {WindowArgs}\r\n");

        // インターネット：部屋を作る（合言葉が画面に出る。10～20秒かかる）
        Write(outputFolder, "ネット_部屋を作る.bat",
            "@echo off\r\n" +
            "rem INTERNET: create a room. The join code appears on screen (takes 10-20 sec).\r\n" +
            $"start \"NETHOST\" {exe} -nethost {WindowArgs}\r\n");

        // インターネット：合言葉を聞いて参加する（別のPCでも、同じPCでもよい）
        Write(outputFolder, "ネット_合言葉で参加.bat",
            "@echo off\r\n" +
            "rem INTERNET: join a room with the join code shown on the host screen\r\n" +
            "set CODE=\r\n" +
            "set /p CODE=Join code: \r\n" +
            "if \"%CODE%\"==\"\" exit /b\r\n" +
            $"start \"NETJOIN\" {exe} -netjoin %CODE% {WindowArgs}\r\n");

        // 画面のボタンで操作したいとき（本番と同じ手順を試すとき）
        Write(outputFolder, "2つ起動するだけ.bat",
            "@echo off\r\n" +
            "rem Just start two windows. Connect with the on-screen buttons.\r\n" +
            $"start \"1\" {exe} {WindowArgs}\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            $"start \"2\" {exe} {WindowArgs}\r\n");
    }

    private static void Write(string folder, string fileName, string contents)
    {
        File.WriteAllText(Path.Combine(folder, fileName), contents);
    }
}
