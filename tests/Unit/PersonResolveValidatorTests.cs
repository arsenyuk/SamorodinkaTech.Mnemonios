using FluentAssertions;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Validation;
using Xunit;

namespace Mnemonios.UnitTests;

public class PersonResolveValidatorTests
{
    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        var request = CreateRequest();
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingLastName_ReturnsInvalid()
    {
        var request = CreateRequest(lastName: "");
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Фамилия"));
    }

    [Fact]
    public void Validate_MissingFirstName_ReturnsInvalid()
    {
        var request = CreateRequest(firstName: "");
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Имя"));
    }

    [Fact]
    public void Validate_MissingSourceSystemId_ReturnsInvalid()
    {
        var request = CreateRequest(sourceSystemId: "");
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Идентификатор внешней системы"));
    }

    [Fact]
    public void Validate_MissingExternalPersonId_ReturnsInvalid()
    {
        var request = CreateRequest(externalPersonId: "");
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Идентификатор лица во внешней системе"));
    }

    [Fact]
    public void Validate_InvalidInn_ReturnsNoBlockingError()
    {
        var request = CreateRequest(evidence: new Evidence { Inn = "123456789012" });
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidSnils_ReturnsNoBlockingError()
    {
        var request = CreateRequest(evidence: new Evidence { Snils = "12345678901" });
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DulSeriesWithoutNumber_ReturnsNoBlockingError()
    {
        var request = CreateRequest(evidence: new Evidence { DulSeries = "4510" });
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DulNumberWithoutSeries_ReturnsNoBlockingError()
    {
        var request = CreateRequest(evidence: new Evidence { DulNumber = "123456" });
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDefects_InvalidInn_ReturnsDefect()
    {
        var request = CreateRequest(evidence: new Evidence { Inn = "123456789012" });
        var defects = PersonResolveValidator.ValidateDefects(request);
        defects.Should().Contain(d => d.DefectType == "invalid_inn");
    }

    [Fact]
    public void ValidateDefects_InvalidSnils_ReturnsDefect()
    {
        var request = CreateRequest(evidence: new Evidence { Snils = "12345678901" });
        var defects = PersonResolveValidator.ValidateDefects(request);
        defects.Should().Contain(d => d.DefectType == "invalid_snils");
    }

    [Fact]
    public void ValidateDefects_DulSeriesWithoutNumber_ReturnsDefect()
    {
        var request = CreateRequest(evidence: new Evidence { DulSeries = "4510" });
        var defects = PersonResolveValidator.ValidateDefects(request);
        defects.Should().Contain(d => d.DefectType == "dul_incomplete");
    }

    [Fact]
    public void ValidateDefects_DulNumberWithoutSeries_ReturnsDefect()
    {
        var request = CreateRequest(evidence: new Evidence { DulNumber = "123456" });
        var defects = PersonResolveValidator.ValidateDefects(request);
        defects.Should().Contain(d => d.DefectType == "dul_incomplete");
    }

    [Fact]
    public void ValidateDefects_ValidData_ReturnsEmpty()
    {
        var request = CreateRequest(evidence: new Evidence { Inn = "7707083893", Snils = "12345678964" });
        var defects = PersonResolveValidator.ValidateDefects(request);
        defects.Should().BeEmpty();
    }

    [Theory]
    [InlineData("7707083893")]
    [InlineData("770708389324")]
    public void ValidateInn_ValidInn_ReturnsTrue(string inn)
    {
        InnValidator.Validate(inn).Should().BeTrue();
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    public void ValidateInn_InvalidInn_ReturnsFalse(string inn)
    {
        InnValidator.Validate(inn).Should().BeFalse();
    }

    [Theory]
    [InlineData("12345678964")]
    public void ValidateSnils_ValidSnils_ReturnsTrue(string snils)
    {
        SnilsValidator.Validate(snils).Should().BeTrue();
    }

    [Theory]
    [InlineData("12345678901")]
    public void ValidateSnils_InvalidSnils_ReturnsFalse(string snils)
    {
        SnilsValidator.Validate(snils).Should().BeFalse();
    }

    // =========================================================================
    // DUL type validation tests
    // =========================================================================

    [Fact]
    public void Validate_InvalidDulType_ReturnsInvalid()
    {
        var request = CreateRequest(evidence: new Evidence { DulType = "99" });
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Недопустимый код вида документа"));
    }

    [Fact]
    public void Validate_ValidDulType_ReturnsValid()
    {
        var request = CreateRequest(evidence: new Evidence { DulType = "21" });
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DulType91_RequiresDulTypeName()
    {
        var request = CreateRequest(evidence: new Evidence { DulType = "91", DulTypeName = "" });
        var result = PersonResolveValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("DulTypeName"));
    }

    private static ResolveRequest CreateRequest(
        string firstName = "Иван",
        string lastName = "Иванов",
        string sourceSystemId = "TEST",
        string externalPersonId = "ext-001",
        Evidence? evidence = null)
    {
        return new ResolveRequest
        {
            FirstName = firstName,
            LastName = lastName,
            SourceSystemId = sourceSystemId,
            ExternalPersonId = externalPersonId,
            Evidence = evidence
        };
    }
}
