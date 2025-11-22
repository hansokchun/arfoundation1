using UnityEngine;
using UnityEngine.SceneManagement;

public class StartPreview : MonoBehaviour
{
    public void OnStartPreview()
    {
        SceneManager.LoadScene("PreviewLoading");
    }
}
