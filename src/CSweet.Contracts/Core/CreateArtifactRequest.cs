using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record CreateArtifactRequest(
    Guid? TaskId,
    Guid? TaskRunId,
    [Required] int Type,
    [Required] string Title,
    [Required] string Content,
    int Version,
    int ApprovalStatus);
