using UnityEngine;
using UnityEngine.SceneManagement;

public class StartScan : MonoBehaviour
{
    public void OnStartScan()
    {
        SceneManager.LoadScene("ScanScene");
    }
}
