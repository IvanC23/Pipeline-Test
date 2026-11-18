using System;
using System.Collections.Generic;
using NUnit.Framework;
using StarCollector.Character;

namespace StarCollector.Tests.EditMode.Character
{
    public class RigBoneMapperTests
    {
        private static readonly string[] CompleteRig = { "Root", "Hips", "Spine", "Neck", "Head", "LeftHand", "RightHand" };

        [Test]
        [Category("FUNC-03")]
        public void Map_AllDefaultBonesPresent_MapsEveryRole()
        {
            var result = new RigBoneMapper().Map(CompleteRig);

            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.MissingRoles, Is.Empty);
            Assert.That(result.MappedBones.Count, Is.EqualTo(RigBoneMapper.DefaultBoneNames.Count));
            Assert.That(result.MappedBones[BoneRole.Head], Is.EqualTo("Head"));
        }

        [Test]
        [Category("FUNC-03")]
        public void Map_NamesDifferInCase_MapsWithTheRigSpelling()
        {
            var result = new RigBoneMapper().Map(new[] { "hips", "SPINE", "head", "leftHand", "rightHAND" });

            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.MappedBones[BoneRole.Hips], Is.EqualTo("hips"));
            Assert.That(result.MappedBones[BoneRole.Spine], Is.EqualTo("SPINE"));
        }

        [Test]
        [Category("FUNC-03")]
        public void Map_SameNameTwice_FirstOccurrenceWins()
        {
            var result = new RigBoneMapper().Map(new[] { "Hips", "Spine", "Head", "HEAD", "LeftHand", "RightHand" });

            Assert.That(result.MappedBones[BoneRole.Head], Is.EqualTo("Head"));
        }

        [Test]
        [Category("FUNC-03")]
        public void Map_CustomNames_UsesTheGivenNames()
        {
            var mapper = new RigBoneMapper(new Dictionary<BoneRole, string>
            {
                [BoneRole.Head] = "Rig_Head",
                [BoneRole.Hips] = "Rig_Pelvis",
            });

            var result = mapper.Map(new[] { "Rig_Pelvis", "Rig_Head", "Head" });

            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.MappedBones[BoneRole.Head], Is.EqualTo("Rig_Head"));
            Assert.That(result.MappedBones[BoneRole.Hips], Is.EqualTo("Rig_Pelvis"));
        }

        [Test]
        [Category("RISK-02")]
        public void Map_BoneMissing_ReportsTheRoleAndIncompleteRig()
        {
            var result = new RigBoneMapper().Map(new[] { "Hips", "Spine", "Head", "RightHand" });

            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.MissingRoles, Is.EquivalentTo(new[] { BoneRole.LeftHand }));
            Assert.That(result.MappedBones.ContainsKey(BoneRole.LeftHand), Is.False);
        }

        [Test]
        [Category("RISK-02")]
        public void Map_EmptyRig_ReportsEveryRoleAsMissing()
        {
            var result = new RigBoneMapper().Map(new string[0]);

            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.MissingRoles, Is.EquivalentTo(RigBoneMapper.DefaultBoneNames.Keys));
        }

        [Test]
        [Category("RISK-02")]
        public void Map_EmptyBoneNames_AreIgnored()
        {
            var result = new RigBoneMapper(new Dictionary<BoneRole, string> { [BoneRole.Head] = "Head" })
                .Map(new[] { null, "", "Hips" });

            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.MissingRoles, Is.EquivalentTo(new[] { BoneRole.Head }));
        }

        [Test]
        [Category("RISK-02")]
        public void Constructor_EmptyExpectedName_Throws([Values(null, "", "   ")] string boneName)
        {
            Assert.Throws<ArgumentException>(() =>
                new RigBoneMapper(new Dictionary<BoneRole, string> { [BoneRole.Head] = boneName }));
        }

        [Test]
        [Category("RISK-02")]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new RigBoneMapper(null));
            Assert.Throws<ArgumentNullException>(() => new RigBoneMapper().Map(null));
        }
    }
}
