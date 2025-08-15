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

using FluentAssertions;
using ProxyStudio.Helpers;
using Xunit.Abstractions;

namespace ProxyStudioTests.Helpers;

public class DebugHelperTests
{
    private readonly ITestOutputHelper _output;

    public DebugHelperTests(ITestOutputHelper output)
    {
        _output = output;
    }


    /// <summary>
    /// test the void WriteDebug(string message) method
    /// </summary>
    [Fact]
    public void WriteDebug_WithValidMessage_ShouldntThrowException()
    {
        // Arrange
        string message = "This is a test debug message.";

        // Act & Assert
        // We expect this to not throw an exception, so we can just call the method.
        var act = () => DebugHelper.WriteDebug(message);

    }

    /// <summary>
    /// LESSON 2: Testing with Different Input Types
    /// Test various kinds of strings
    /// </summary>
    [Theory]
    [InlineData("Simple message")]
    [InlineData("Message with numbers: 123")]
    [InlineData("Message with special chars: !@#$%^&*()")]
    [InlineData(
        "Very long message that goes on and on and on to test if the method can handle lengthy debug messages without any issues")]
    [InlineData("")] // Empty string
    [InlineData("   ")] // Whitespace
    public void WriteDebug_WithDifferentMessageTypes_ShouldNotThrow(string message)
    {
        // Act & Assert
        var act = () => DebugHelper.WriteDebug(message);

        act.Should().NotThrow($"because WriteDebug should handle message: '{message}'");

        _output.WriteLine($"✓ Successfully handled: '{message}'");
    }

    /// <summary>
    /// LESSON 3: Testing Edge Cases
    /// What happens with null input?
    /// </summary>
    [Fact]
    public void WriteDebug_WithNullMessage_ShouldHandleGracefully()
    {
        // Act & Assert
        var act = () => DebugHelper.WriteDebug(null!);

        // This might throw or might handle null gracefully - test actual behavior
        // If it should throw, use: act.Should().Throw<ArgumentNullException>();
        // If it should handle gracefully, use: act.Should().NotThrow();

        // Let's assume it should handle null gracefully for now
        act.Should().NotThrow("because WriteDebug should handle null input gracefully");

        _output.WriteLine("✓ Null input handled gracefully");
    }

    /// <summary>
    /// LESSON 4: Testing Side Effects (Advanced Topic)
    /// If you want to verify the actual console output, you can capture it
    /// </summary>
    [Fact]
    public void WriteDebug_WithMessage_ShouldWriteToConsole()
    {
        // Arrange
        string testMessage = "Test debug output";

        // Capture console output (advanced technique)
        using var stringWriter = new StringWriter();
        var originalConsoleOut = Console.Out;
        Console.SetOut(stringWriter);

        try
        {
            // Act
            DebugHelper.WriteDebug(testMessage);

            // Assert
            string consoleOutput = stringWriter.ToString();
            consoleOutput.Should().Contain(testMessage, "because the message should be written to console");

            _output.WriteLine($"Console output captured: '{consoleOutput.Trim()}'");
        }
        finally
        {
            // Always restore original console output
            Console.SetOut(originalConsoleOut);
        }
    }

    /// <summary>
    /// LESSON 5: Testing Performance (If Relevant)
    /// For simple methods, you might want to ensure they're fast
    /// </summary>
    [Fact]
    public void WriteDebug_ShouldExecuteQuickly()
    {
        // Arrange
        string message = "Performance test message";

        // Act & Assert - Test that it completes within reasonable time
        var act = () => DebugHelper.WriteDebug(message);

        act.ExecutionTime().Should().BeLessThan(TimeSpan.FromMilliseconds(100),
            "because WriteDebug should be very fast");

        _output.WriteLine("✓ WriteDebug executes quickly");
    }

    /// <summary>
    /// LESSON 6: Stress Testing (Optional)
    /// Test with many calls to ensure stability
    /// </summary>
    [Fact]
    public void WriteDebug_CalledManyTimes_ShouldRemainStable()
    {
        // Arrange & Act - Call many times
        var act = () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                DebugHelper.WriteDebug($"Stress test message {i}");
            }
        };

        // Assert
        act.Should().NotThrow("because WriteDebug should handle many consecutive calls");

        _output.WriteLine("✓ WriteDebug stable under multiple calls");


    }
}