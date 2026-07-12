using CSweet.Contracts.Planning;
using CSweet.Domain.Planning;

namespace CSweet.Infrastructure.Planning;

internal static class PlanningMappers
{
    #region PlanningTask

    public static PlanningTaskResponse ToResponse(this PlanningTask task)
    {
        return new PlanningTaskResponse(
            task.Id,
            task.TaskKey,
            task.DisplayName,
            task.Status.ToString(),
            Truncate(task.OutputContent, 500),
            task.FailureMessage,
            task.InputTokenCount,
            task.OutputTokenCount,
            task.DurationMs,
            task.SortOrder,
            task.IsRequired,
            task.StartedAt,
            task.CompletedAt);
    }

    #endregion

    #region PlanningDocument

    public static PlanningDocumentResponse ToResponse(this PlanningDocument doc)
    {
        return new PlanningDocumentResponse(
            doc.Id,
            doc.Title,
            doc.DocumentType,
            doc.Content,
            doc.Summary,
            doc.Version,
            doc.IsLatest,
            doc.GeneratedAt,
            doc.CreatedAt,
            doc.UpdatedAt);
    }

    #endregion

    #region PlanningWorkflow

    public static PlanningWorkflowResponse ToResponse(this PlanningWorkflow workflow)
    {
        return new PlanningWorkflowResponse(
            workflow.Id,
            workflow.Key,
            workflow.DisplayName,
            workflow.Description,
            workflow.IsEnabled,
            workflow.SortOrder);
    }

    #endregion

    static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
