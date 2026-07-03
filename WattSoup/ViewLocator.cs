using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WattSoup.ViewModels;

namespace WattSoup;

/// <summary>
/// Maps a ViewModel type (…ViewModels.FooViewModel) to its View (…Views.Foo) by naming convention.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!
            .Replace("ViewModel", "View", StringComparison.Ordinal)
            .Replace(".Views.", ".Views.", StringComparison.Ordinal);

        var type = Type.GetType(name);

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
