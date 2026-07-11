using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record CreateApprovalRequest(
    [Required] int Status,
    string? Comment);
