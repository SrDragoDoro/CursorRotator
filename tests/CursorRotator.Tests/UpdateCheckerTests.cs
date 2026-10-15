using CursorRotator.Core;
using Xunit;

namespace CursorRotator.Tests;

/// <summary>Comparación de versiones del comprobador de actualizaciones (pura, sin red).</summary>
public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v2.6.0", "2.5.0", true)]
    [InlineData("2.6.0", "2.5.0", true)]
    [InlineData("v2.5.0", "2.5.0", false)]
    [InlineData("2.4.9", "2.5.0", false)]
    [InlineData("v3", "2.5.0", true)]
    [InlineData("v2.5.1", "2.5.0", true)]
    [InlineData("abc", "2.5.0", false)]
    [InlineData("", "2.5.0", false)]
    public void IsNewer_ComparaVersiones(string tag, string current, bool expected) =>
        Assert.Equal(expected, UpdateChecker.IsNewer(tag, current));

    [Fact]
    public void CurrentVersion_NoEstaVacia() =>
        Assert.False(string.IsNullOrWhiteSpace(UpdateChecker.CurrentVersion));
}
