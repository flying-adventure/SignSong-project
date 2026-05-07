using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingController : MonoBehaviour
{
    public Image loadingBarFill;
    public float loadingDuration = 3f;
    public string nextSceneName = "Login_page";

    private void Start()
    {
        StartCoroutine(LoadRoutine());
    }

    IEnumerator LoadRoutine()
    {
        float elapsed = 0f;

        while (elapsed < loadingDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / loadingDuration;
            loadingBarFill.fillAmount = progress;
            yield return null;
        }

        loadingBarFill.fillAmount = 1f;

        SceneManager.LoadScene(nextSceneName);
    }
}