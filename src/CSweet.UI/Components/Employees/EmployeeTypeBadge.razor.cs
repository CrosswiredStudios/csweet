using Microsoft.AspNetCore.Components;

namespace CSweet.UI.Components.Employees;

public partial class EmployeeTypeBadge
{
    [Parameter]
    public bool IsAgent { get; set; }
}
