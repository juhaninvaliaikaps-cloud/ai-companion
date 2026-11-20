using AICompanion.Memory;

namespace AICompanion.Settings
{
    /// <summary>
    /// The memory summary on the Privacy tab. Memory reset is Start over in the persona
    /// section; there is deliberately no selective delete while the product is in testing.
    /// </summary>
    public sealed partial class SettingsPanel
    {
        void BuildMemorySection()
        {
            var store = MemoryRuntime.Store;
            Note("Memories on this PC");
            Note(MemoryViewerModel.Summary(store.Entries));
            Note("Stored under ProjectLeia, outside the repository. Memories made in private mode are never sent to a cloud model.");
            Note("To clear this persona's memories, use Start over in the persona section.");
        }
    }
}
