using System.Collections.Generic;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Reads the loaded avatar's blendshapes and expression preset map.
    /// </summary>
    public interface IAvatarDescriptorService
    {
        AvatarDescriptor Current { get; }

        bool TryGetBlendShapeNames(out IReadOnlyList<string> names);

        bool TryGetExpression(ExpressionPreset preset, out IReadOnlyList<string> blendShapeNames);
    }
}
