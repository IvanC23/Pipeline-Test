using System;
using System.Collections.Generic;

namespace StarCollector.Character
{
    /// <summary>
    /// Maps the bones of a character rig to the roles the game needs, by name.
    /// Names are compared ignoring case. A role whose bone is not found is
    /// reported as missing: the caller decides whether the rig can be used.
    /// </summary>
    public sealed class RigBoneMapper
    {
        /// <summary>Expected bone name for every role, used when no names are given.</summary>
        public static readonly IReadOnlyDictionary<BoneRole, string> DefaultBoneNames =
            new Dictionary<BoneRole, string>
            {
                [BoneRole.Hips] = "Hips",
                [BoneRole.Spine] = "Spine",
                [BoneRole.Head] = "Head",
                [BoneRole.LeftHand] = "LeftHand",
                [BoneRole.RightHand] = "RightHand",
            };

        private readonly IReadOnlyDictionary<BoneRole, string> expectedBoneNames;

        public RigBoneMapper()
            : this(DefaultBoneNames)
        {
        }

        public RigBoneMapper(IReadOnlyDictionary<BoneRole, string> expectedBoneNames)
        {
            if (expectedBoneNames == null)
            {
                throw new ArgumentNullException(nameof(expectedBoneNames));
            }

            foreach (var pair in expectedBoneNames)
            {
                if (string.IsNullOrWhiteSpace(pair.Value))
                {
                    throw new ArgumentException($"The expected bone name for {pair.Key} is empty.", nameof(expectedBoneNames));
                }
            }

            this.expectedBoneNames = expectedBoneNames;
        }

        /// <param name="rigBoneNames">Names of all the bones in the rig.</param>
        public BoneMappingResult Map(IEnumerable<string> rigBoneNames)
        {
            if (rigBoneNames == null)
            {
                throw new ArgumentNullException(nameof(rigBoneNames));
            }

            // First occurrence wins, with the spelling used in the rig.
            var rigNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in rigBoneNames)
            {
                if (!string.IsNullOrEmpty(name) && !rigNames.ContainsKey(name))
                {
                    rigNames.Add(name, name);
                }
            }

            var mapped = new Dictionary<BoneRole, string>();
            var missing = new List<BoneRole>();

            foreach (var pair in expectedBoneNames)
            {
                if (rigNames.TryGetValue(pair.Value, out var rigName))
                {
                    mapped.Add(pair.Key, rigName);
                }
                else
                {
                    missing.Add(pair.Key);
                }
            }

            return new BoneMappingResult(mapped, missing);
        }
    }
}
