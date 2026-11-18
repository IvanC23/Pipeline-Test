using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarCollector.Tests.PlayMode
{
    /// <summary>
    /// Smoke test: every scene in the Build Settings loads by name and runs a
    /// few frames without unexpected errors or exceptions.
    /// </summary>
    public class SceneSmokeTests
    {
        // Frames to run after loading, so that Awake, Start and the first
        // Update calls of the scene objects have run.
        private const int FramesAfterLoad = 5;

        // Known messages per scene name. Each entry must be justified: an
        // expected message is accepted, any other error still fails the test.
        // Never silence errors with LogAssert.ignoreFailingMessages.
        private static readonly Dictionary<string, (LogType Type, string Message)[]> ExpectedMessages =
            new Dictionary<string, (LogType Type, string Message)[]>
            {
            };

        // Read from the Build Settings, so a new scene is tested without
        // changing this file. Only enabled scenes are listed.
        private static IEnumerable<string> BuildSceneNames()
        {
            for (var index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                yield return Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(index));
            }
        }

        [UnityTest]
        public IEnumerator Scene_LoadsWithoutUnexpectedErrors([ValueSource(nameof(BuildSceneNames))] string sceneName)
        {
            // Expectations are declared before the code that logs them.
            if (ExpectedMessages.TryGetValue(sceneName, out var expectedMessages))
            {
                foreach (var (type, message) in expectedMessages)
                {
                    LogAssert.Expect(type, message);
                }
            }

            var loading = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(loading, Is.Not.Null, $"Scene '{sceneName}' could not be loaded by name.");
            yield return loading;

            for (var frame = 0; frame < FramesAfterLoad; frame++)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName),
                $"Scene '{sceneName}' is not the active scene after loading.");

            // Explicit check after the frames above: any error or exception
            // logged by the scene and not expected fails the test here.
            LogAssert.NoUnexpectedReceived();
        }
    }
}
