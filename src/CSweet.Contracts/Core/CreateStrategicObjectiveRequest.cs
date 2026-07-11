using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record CreateStrategicObjectiveRequest(
    [Required] string Title,
    string Description,
    int Status,
    DateTimeOffset? TargetDate);
