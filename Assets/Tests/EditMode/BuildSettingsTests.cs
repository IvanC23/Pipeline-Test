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

        // Deliberate failure to prove that a failing test blocks the pull request.
        // Remove after the check.
        [Test]
        public void DeliberateFailure_BlocksPullRequest()
        {
            Assert.That(EditorBuildSettings.scenes.Length, Is.EqualTo(-1),
                "Deliberate failure: this test must turn the unity-tests check red.");
        }
    }
}
