using CSweet.UI.Components.Employees.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CSweet.UI.Components.Employees;

public partial class EmployeeActionMenu
{
    [Parameter, EditorRequired]
    public EmployeeViewModel Employee { get; set; } = default!;

    [Parameter]
    public bool ShowDirectChat { get; set; } = true;

    [Parameter]
    public EventCallback<EmployeeActionRequest> ActionRequested { get; set; }

    protected IReadOnlyList<EmployeeAction> MenuActions => Employee.Actions
        .Where(x => !ShowDirectChat || x != EmployeeAction.OpenChat)
        .ToArray();

    protected Task RequestAsync(EmployeeAction action) =>
        ActionRequested.InvokeAsync(new EmployeeActionRequest(action, Employee.Id));

    protected static string Label(EmployeeAction action) => action switch
    {
        EmployeeAction.OpenChat => "Open chat",
        EmployeeAction.StartRuntime => "Start runtime",
        EmployeeAction.StopRuntime => "Stop runtime",
        EmployeeAction.OpenConsole => "Open console",
        EmployeeAction.Configure => "Configure agent",
        EmployeeAction.OpenMemory => "Explore memory",
        EmployeeAction.ChangeRole => "Change role",
        EmployeeAction.Fire => "Fire employee",
        _ => action.ToString()
    };

    protected static string Icon(EmployeeAction action) => action switch
    {
        EmployeeAction.OpenChat => Icons.Material.Outlined.ChatBubbleOutline,
        EmployeeAction.StartRuntime => Icons.Material.Outlined.PlayCircle,
        EmployeeAction.StopRuntime => Icons.Material.Outlined.StopCircle,
        EmployeeAction.OpenConsole => Icons.Material.Outlined.Terminal,
        EmployeeAction.Configure => Icons.Material.Outlined.Settings,
        EmployeeAction.OpenMemory => Icons.Material.Outlined.Psychology,
        EmployeeAction.ChangeRole => Icons.Material.Outlined.Badge,
        EmployeeAction.Fire => Icons.Material.Outlined.PersonRemove,
        _ => Icons.Material.Outlined.MoreHoriz
    };
}
