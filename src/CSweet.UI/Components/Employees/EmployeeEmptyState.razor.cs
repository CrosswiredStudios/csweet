using Microsoft.AspNetCore.Components;

namespace CSweet.UI.Components.Employees;

public partial class EmployeeEmptyState
{
    [Parameter]
    public EventCallback HireRequested { get; set; }
}
