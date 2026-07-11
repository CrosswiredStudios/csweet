using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record CreateTaskRunRequest(
    Guid? WorkerId,
    string? InputJson);
