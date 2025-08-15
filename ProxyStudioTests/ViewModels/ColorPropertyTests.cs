// ProxyStudio - A cross-platform proxy management application.
// Copyright (C) 2025 James Ponter
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.
// 
// 

using Avalonia.Media;
using FluentAssertions;
using ProxyStudio.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace ProxyStudioTests.ViewModels;

public class ColorPropertyTests
{
    private  readonly ITestOutputHelper output;

    public  ColorPropertyTests(ITestOutputHelper output)
    {
        this.output = output;
    }
    
    [Fact]
    public void Constructor_CreatesObjectWithValidState()
    {
        // Arrange & Act
        var colorProperty = new ColorProperty("Primary", "#FF0000", "Red color");
        
        // Assert
        colorProperty.Name.Should().Be("Primary");
        colorProperty.Description.Should().Be("Red color");
        colorProperty.ColorBrush.Should().NotBeNull();
        
        // The color should be red (regardless of exact hex format)
        colorProperty.Color.R.Should().Be(255);
        colorProperty.Color.G.Should().Be(0);
        colorProperty.Color.B.Should().Be(0);
    }
    
    
    [Fact]
    public void HexValue_WithValidColor_UpdatesColorComponents()
    {
        // Arrange
        var colorProperty = new ColorProperty("Test", "#000000", "Test");
        
        // Act - Set to green
        colorProperty.HexValue = "#00FF00";
        
        // Assert - Check the RGB components
        colorProperty.Color.R.Should().Be(0, "Red component should be 0");
        colorProperty.Color.G.Should().Be(255, "Green component should be 255");
        colorProperty.Color.B.Should().Be(0, "Blue component should be 0");
        
        output.WriteLine($"Color components: R={colorProperty.Color.R}, G={colorProperty.Color.G}, B={colorProperty.Color.B}");
    }
    
    [Fact]
    public void HexValue_WithInvalidColor_DoesNotChangeColorComponents()
    {
        // Arrange
        var colorProperty = new ColorProperty("Test", "#FF0000", "Test");
        var originalR = colorProperty.Color.R;
        var originalG = colorProperty.Color.G;
        var originalB = colorProperty.Color.B;
        
        // Act - Try to set invalid color
        colorProperty.HexValue = "invalid";
        
        // Assert - Color components should be unchanged
        colorProperty.Color.R.Should().Be(originalR, "Red component should not change with invalid input");
        colorProperty.Color.G.Should().Be(originalG, "Green component should not change with invalid input");
        colorProperty.Color.B.Should().Be(originalB, "Blue component should not change with invalid input");
        
        output.WriteLine($"After invalid input - Color components: R={colorProperty.Color.R}, G={colorProperty.Color.G}, B={colorProperty.Color.B}");
    }
    
    [Theory]
    [InlineData("#FF0000", 255, 0, 0)]    // Red
    [InlineData("#00FF00", 0, 255, 0)]    // Green  
    [InlineData("#0000FF", 0, 0, 255)]    // Blue
    [InlineData("#FFFFFF", 255, 255, 255)] // White
    [InlineData("#000000", 0, 0, 0)]      // Black
    public void HexValue_WithDifferentValidColors_SetsCorrectRGBComponents(
        string hexInput, byte expectedR, byte expectedG, byte expectedB)
    {
        // Arrange
        var colorProperty = new ColorProperty("Test", "#123456", "Test");
        
        // Act
        colorProperty.HexValue = hexInput;
        
        // Assert
        colorProperty.Color.R.Should().Be(expectedR);
        colorProperty.Color.G.Should().Be(expectedG);
        colorProperty.Color.B.Should().Be(expectedB);
        
        output.WriteLine($"{hexInput} -> R={colorProperty.Color.R}, G={colorProperty.Color.G}, B={colorProperty.Color.B}");
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("FF0000")]     // Missing #
    [InlineData("#FF00")]      // Too short
    [InlineData("#FF0000FF")]  // Too long  
    [InlineData("#GG0000")]    // Invalid character
    public void HexValue_WithInvalidFormats_PreservesOriginalColor(string invalidInput)
    {
        // Arrange
        var colorProperty = new ColorProperty("Test", "#123456", "Test");
        var originalColor = colorProperty.Color;
        
        // Act
        colorProperty.HexValue = invalidInput;
        
        // Assert
        colorProperty.Color.Should().Be(originalColor, $"Invalid input '{invalidInput}' should not change the color");
        
        output.WriteLine($"Invalid: '{invalidInput}' -> Color remains: {colorProperty.Color}");
    }
    
    [Fact]
    public void ColorBrush_ReturnsCorrectSolidColorBrush()
    {
        // Arrange
        var colorProperty = new ColorProperty("Test", "#FF0000", "Test");
        
        // Act
        var brush = colorProperty.ColorBrush;
        
        // Assert
        brush.Should().BeOfType<SolidColorBrush>();
        brush.Color.Should().Be(colorProperty.Color);
        
        // Change color and verify brush reflects the change
        colorProperty.HexValue = "#00FF00";
        var newBrush = colorProperty.ColorBrush;
        newBrush.Color.Should().Be(colorProperty.Color, "ColorBrush should reflect current color");
    }
}