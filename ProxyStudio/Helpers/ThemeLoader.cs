/*
ProxyStudio - A cross-platform proxy management application.
Copyright (C) 2025 James Ponter

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
*/

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