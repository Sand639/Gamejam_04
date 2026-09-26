using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// **タイトル画面。** 「スタート」を押すと指スマのシーンへ進む。
///
/// マウスでクリックするほか、**Enter／スペース（コントローラーなら決定ボタン）でも押せる**。
/// 起動した時点でスタートのボタンを選んだ状態にしておくため。
/// </summary>
public class TitleMenu : MonoBehaviour
{
    [Tooltip("スタートで進むシーンの名前。Build Profiles（ビルドの一覧）に入っている必要がある")]
    [SerializeField] private string gameSceneName = "Yubisuma";

    [Tooltip("スタートのボタン。最初から選んだ状態にして、キーボードでも押せるようにする")]
    [SerializeField] private Button startButton;

    private void Start()
    {
        if (startButton == null)
        {
            return;
        }

        startButton.onClick.AddListener(StartGame);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }
    }

    /// <summary>指スマのシーンへ進む。</summary>
    public void StartGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"シーン「{gameSceneName}」がビルドの一覧に入っていないため、進めません。");
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }
}
