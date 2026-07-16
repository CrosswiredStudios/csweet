using CSweet.UI.Components.Employees.Models;
using Microsoft.AspNetCore.Components;

namespace CSweet.UI.Components.Employees;

public partial class EmployeeGraphView
{
    [Parameter]
    public IReadOnlyList<EmployeeViewModel> Employees { get; set; } = [];

    [Parameter]
    public Guid? SelectedId { get; set; }

    [Parameter]
    public EventCallback<Guid> SelectedIdChanged { get; set; }

    [Parameter]
    public int Degrees { get; set; } = 2;

    [Parameter]
    public EventCallback<int> DegreesChanged { get; set; }

    [Parameter]
    public EventCallback<EmployeeActionRequest> ActionRequested { get; set; }

    protected EmployeeGraphModel Graph => EmployeeHierarchyService.Build(Employees, SelectedId, Degrees);
    protected EmployeeViewModel? SelectedEmployee => Employees.FirstOrDefault(x => x.Id == SelectedId);
    protected string ViewBox => $"0 0 {Graph.Width:0} {Graph.Height:0}";

    protected Task SelectAsync(Guid id) => SelectedIdChanged.InvokeAsync(id);
    protected Task ChangeDegreesAsync(int value) => DegreesChanged.InvokeAsync(value);

    protected static string EdgePath(EmployeeGraphLayoutEdge edge)
    {
        var startY = edge.From.Y + 52;
        var endY = edge.To.Y - 52;
        var middleY = startY + (endY - startY) / 2;
        return $"M {edge.From.X:0.#} {startY:0.#} V {middleY:0.#} H {edge.To.X:0.#} V {endY:0.#}";
    }
}
