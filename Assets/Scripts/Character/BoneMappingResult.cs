using System.Collections.Generic;

namespace StarCollector.Character
{
    /// <summary>
    /// Outcome of mapping a rig: the bone found for each role and the roles
    /// with no matching bone.
    /// </summary>
    public sealed class BoneMappingResult
    {
        /// <summary>Bone name found in the rig for each mapped role.</summary>
        public IReadOnlyDictionary<BoneRole, string> MappedBones { get; }

        /// <summary>Roles whose expected bone is not in the rig.</summary>
        public IReadOnlyList<BoneRole> MissingRoles { get; }

        /// <summary>True when every required role has a bone.</summary>
        public bool IsComplete => MissingRoles.Count == 0;

        public BoneMappingResult(IReadOnlyDictionary<BoneRole, string> mappedBones, IReadOnlyList<BoneRole> missingRoles)
        {
            MappedBones = mappedBones;
            MissingRoles = missingRoles;
        }
    }
}
