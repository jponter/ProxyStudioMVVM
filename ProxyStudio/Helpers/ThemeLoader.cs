using System;
using System.Reflection;
using Avalonia.Styling;

namespace ProxyStudio.Helpers;

public static class ThemeLoader
{
    public static Styles LoadFromXaml(string xaml)
    {
        var asm = typeof(Avalonia.Application).Assembly;
        var loaderType = Type.GetType("Avalonia.Markup.Xaml.XamlIl.RuntimeXamlLoader, Avalonia.Markup.Xaml");

        if (loaderType == null)
            throw new InvalidOperationException("RuntimeXamlLoader type not found.");

        var parseMethod = loaderType.GetMethod("Parse", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        if (parseMethod == null)
            throw new InvalidOperationException("RuntimeXamlLoader.Parse method not found.");

        var result = parseMethod.Invoke(null, new object[] { xaml });

        if (result is Styles styles)
            return styles;

        throw new InvalidOperationException("Parsed object is not of type <Styles>");
    }
}