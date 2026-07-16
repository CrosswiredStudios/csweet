using CSweet.UI.Components.Employees.Models;
using Microsoft.AspNetCore.Components;

namespace CSweet.UI.Components.Employees;

public partial class EmployeeAvatar
{
    [Parameter, EditorRequired]
    public EmployeeViewModel Employee { get; set; } = default!;
}
