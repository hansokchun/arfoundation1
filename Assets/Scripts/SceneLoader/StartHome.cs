using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class StartHome : MonoBehaviour
{
    public void OnStartHome()
    {
        SceneManager.LoadScene("HomeScene");
    }
}
