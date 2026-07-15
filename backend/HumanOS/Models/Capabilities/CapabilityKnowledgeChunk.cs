using Microsoft.Data.SqlTypes;

namespace HumanOS.Models.Capabilities;

public sealed class CapabilityKnowledgeChunk
{
    public Guid CapabilityKnowledgeChunkId { get; set; }

    public Guid CapabilityId { get; set; }

    public Guid? CapabilityModuleId { get; set; }

    public int SortOrder { get; set; }

    public string Content { get; set; } = null!;

    public SqlVector<float> Embedding { get; set; }

    public DateTime CreatedDate { get; set; }

    public Capability Capability { get; set; } = null!;

    public CapabilityModule? CapabilityModule { get; set; }
}
