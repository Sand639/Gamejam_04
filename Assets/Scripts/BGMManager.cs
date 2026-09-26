using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip titleBGM;
    public AudioClip gameBGM;
    public AudioClip resultBGM;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
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
