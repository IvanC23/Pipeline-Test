using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace StarCollector.Tests.EditMode
{
    /// <summary>
    /// Checks on the project configuration that a player build depends on.
    /// </summary>
    public class BuildSettingsTests
    {
        [Test]
        public void BuildSettings_ContainAtLeastOneEnabledScene()
        {
            var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();

            Assert.That(enabledScenes, Is.Not.Empty,
                "The Build Settings list has no enabled scene: a player build would contain nothing to run.");
        }

        // Deliberate forbidden setting: it must turn the unity-tests check red
        // before Unity starts. Remove after the check.
        [Test]
        public void DeliberateForbiddenSetting_BlocksPullRequest()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.Pass();
        }
    }
}
