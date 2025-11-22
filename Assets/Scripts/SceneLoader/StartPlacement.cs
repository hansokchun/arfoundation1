using UnityEngine;
using UnityEngine.SceneManagement;

public class StartPlacement : MonoBehaviour
{
    public void OnStartPlacement()
    {
        SceneManager.LoadScene("furniture stack");
    }
}
