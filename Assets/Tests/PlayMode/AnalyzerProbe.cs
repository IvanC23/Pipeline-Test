using UnityEngine;

namespace StarCollector.Tests.PlayMode
{
    // Temporary probe: violates rule UNT0008 on purpose (expected to fail).
    public class AnalyzerProbe : MonoBehaviour
    {
        [SerializeField] private GameObject target;

        private void Start()
        {
            target?.SetActive(false);
        }
    }
}
