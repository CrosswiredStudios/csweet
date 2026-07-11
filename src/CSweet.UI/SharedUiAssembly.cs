using System.Reflection;

namespace CSweet.UI;

public static class SharedUiAssembly
{
    public static Assembly Value => typeof(SharedUiAssembly).Assembly;
}
