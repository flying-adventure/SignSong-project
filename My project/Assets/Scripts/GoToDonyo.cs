using UnityEngine;
using UnityEngine.SceneManagement;

public class GoToDongyo : MonoBehaviour
{
    public void LoadDongyoList()
    {
        SceneManager.LoadScene("Dongyo_list");
    }
}
