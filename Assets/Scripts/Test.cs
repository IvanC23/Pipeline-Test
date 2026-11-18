using UnityEngine;
using UnityEngine.SceneManagement;

public class Test : MonoBehaviour
{
    int numeroPrimo = 0;
    float numberOfSeconds = 0.0f;

    // Deliberate error to prove that the scene smoke test fails on an
    // unexpected error. To be removed after the check.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LogErrorWhenSampleSceneLoads()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "SampleScene")
            {
                Debug.LogError("Deliberate error: SampleScene loaded.");
            }
        };
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
