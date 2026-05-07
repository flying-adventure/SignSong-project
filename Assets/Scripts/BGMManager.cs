using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    private AudioSource bgmSource;
    private bool isBgmOn = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            bgmSource = GetComponent<AudioSource>();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        CheckScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckScene(scene.name);
    }

    private void CheckScene(string sceneName)
    {
        if (sceneName == "Gayo_game")
        {
            bgmSource.Stop();
        }
        else
        {
            if (isBgmOn && !bgmSource.isPlaying)
                bgmSource.Play();
        }
    }

    public void ToggleBGM()
    {
        isBgmOn = !isBgmOn;

        if (isBgmOn)
        {
            if (SceneManager.GetActiveScene().name != "Gayo_game")
                bgmSource.Play();
        }
        else
        {
            bgmSource.Stop();
        }
    }

    public bool IsBgmOn()
    {
        return isBgmOn;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}