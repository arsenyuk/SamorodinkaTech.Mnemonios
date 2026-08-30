using FluentAssertions;
using Mnemonios.Infrastructure.Services;
using Xunit;

namespace Mnemonios.UnitTests;

public class NormalizationServiceTests
{
    private readonly NormalizationService _sut = new();

    [Theory]
    [InlineData("Иванов", "ИВАНОВ")]
    [InlineData("иванов", "ИВАНОВ")]
    [InlineData("  Иванов  ", "ИВАНОВ")]
    [InlineData("ИВАНОВ", "ИВАНОВ")]
    public void NormalizeName_UpperCasesAndTrims(string input, string expected)
    {
        var result = _sut.NormalizeName(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Иван  Петр", "ИВАН ПЕТР")]
    [InlineData("Иван   Петр", "ИВАН ПЕТР")]
    [InlineData("  Иван   Петр  ", "ИВАН ПЕТР")]
    public void NormalizeName_CollapsesSpaces(string input, string expected)
    {
        var result = _sut.NormalizeName(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("123456789012", "123456789012")]
    [InlineData("123 456 789 012", "123 456 789 012")]
    [InlineData("123-456-789-012", "123-456-789-012")]
    [InlineData("  123456789012  ", "123456789012")]
    public void NormalizeInn_NormalizesAsName(string input, string expected)
    {
        var result = _sut.NormalizeInn(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("123-456-789 01", "123-456-789 01")]
    [InlineData("12345678901", "12345678901")]
    [InlineData("  12345678901  ", "12345678901")]
    public void NormalizeSnils_NormalizesAsName(string input, string expected)
    {
        var result = _sut.NormalizeSnils(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeDul_CombinesFieldsWithSeparator()
    {
        var result = _sut.NormalizeDul("ПАСПОРТ", "4510", "123456");
        result.Should().Be("ПАСПОРТ|4510|123456");
    }

    [Fact]
    public void NormalizeName_EmptyString_ReturnsEmpty()
    {
        var result = _sut.NormalizeName("");
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void NormalizeName_NullString_ReturnsEmpty()
    {
        var result = _sut.NormalizeName(null!);
        result.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("АБ 12", "АБ12")]
    [InlineData("  АБ 12  ", "АБ12")]
    [InlineData("123 456", "123456")]
    [InlineData("АБ12", "АБ12")]
    [InlineData("аб 12", "АБ12")]
    public void NormalizeDulField_RemovesSpacesAndUpperCases(string input, string expected)
    {
        var result = _sut.NormalizeDulField(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeDulField_EmptyString_ReturnsEmpty()
    {
        var result = _sut.NormalizeDulField("");
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void NormalizeDulField_Null_ReturnsEmpty()
    {
        var result = _sut.NormalizeDulField(null!);
        result.Should().Be(string.Empty);
    }
}
