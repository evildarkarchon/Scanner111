using Avalonia.Controls.Templates;
using Scanner111.GUI.ViewModels;

namespace Scanner111.GUI;

public class ViewLocator : IDataTemplate
{
    /// Builds a user interface view for the given ViewModel type by mapping the ViewModel name to its corresponding View name.
    /// <param name="param">
    ///     The object instance, typically a ViewModel, for which the corresponding View should be located and
    ///     instantiated. Can be null.
    /// </param>
    /// <returns>
    ///     A Control object representing the corresponding View for the given ViewModel. Returns null if the parameter is
    ///     null. Returns a TextBlock indicating "Not Found" if the View type cannot be resolved or instantiated.
    /// </returns>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null) return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "Not Found: " + name };
    }

    /// Determines whether the given data object matches the expected ViewModelBase type.
    /// <param name="data">The object to check, typically a ViewModel or null.</param>
    /// <returns>True if the object is of type ViewModelBase; otherwise, false.</returns>
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}