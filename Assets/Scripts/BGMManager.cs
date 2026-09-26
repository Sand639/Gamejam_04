using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip titleBGM;
    public AudioClip gameBGM;
    public AudioClip resultBGM;

    /// <summary>
    /// いま鳴らしている BGMManager。
    /// タイトル画面のシーンにも置いてあるので、タイトルに戻るたびに2つ目が作られてしまう。
    /// そのままだと BGM が重なって大きくなるため、2つ目以降はすぐ消す（Claude Code が追加）。
    /// </summary>
    public static BGMManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Title")
            ChangeBGM(titleBGM);
        else if (scene.name == "Yubisuma")
            ChangeBGM(gameBGM);
        else if (scene.name == "Result")
            ChangeBGM(resultBGM);
    }

    void ChangeBGM(AudioClip clip)
    {
        if (audioSource.clip != clip)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}
