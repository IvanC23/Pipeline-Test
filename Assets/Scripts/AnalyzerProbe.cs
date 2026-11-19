using UnityEngine;

// Temporary probe in Assembly-CSharp: violates rule UNT0033 on purpose (expected to fail).
public class AnalyzerProbe : MonoBehaviour
{
    private int frames;

    private void update()
    {
        frames++;
    }
}
