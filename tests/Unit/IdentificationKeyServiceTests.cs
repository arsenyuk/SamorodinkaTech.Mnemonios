using FluentAssertions;
using Mnemonios.Domain.DTOs;
using Mnemonios.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mnemonios.UnitTests;

public class IdentificationKeyServiceTests
{
    private const string TestHmacKey = "test_secret_key_for_hmac_computation_32chars!";

    private readonly IdentificationKeyService _sut;
    private readonly NormalizationService _normalizationService = new();

    public IdentificationKeyServiceTests()
    {
        var settings = new HmacSettings { Key = TestHmacKey };
        _sut = new IdentificationKeyService(Options.Create(settings), _normalizationService);
    }

    [Fact]
    public void ComputeKeys_WithAllFields_ReturnsExpectedKeyTypes()
    {
        var request = CreateRequest(evidence: new Evidence { Inn = "7707083893", Snils = "12345678964" });

        var keys = _sut.ComputeKeys(request);

        var keyTypes = keys.Select(k => k.KeyType).ToList();
        keyTypes.Should().Contain("inn");
        keyTypes.Should().Contain("snils");
        keyTypes.Should().Contain("fio");
        keyTypes.Should().Contain("fio_full");
        keyTypes.Should().Contain("inn_fio");
        keyTypes.Should().Contain("snils_fio");
    }

    [Fact]
    public void ComputeKeys_WithOnlyFio_ReturnsFioKeys()
    {
        var request = CreateRequest();

        var keys = _sut.ComputeKeys(request);

        var keyTypes = keys.Select(k => k.KeyType).ToList();
        keyTypes.Should().Contain("fio");
        keyTypes.Should().Contain("fio_full");
        keyTypes.Should().NotContain("inn");
        keyTypes.Should().NotContain("snils");
    }

    [Fact]
    public void ComputeKeys_Deterministic_SameInputSameOutput()
    {
        var request = CreateRequest(evidence: new Evidence { Inn = "7707083893", Snils = "12345678964" });

        var keys1 = _sut.ComputeKeys(request);
        var keys2 = _sut.ComputeKeys(request);

        keys1.Should().HaveSameCount(keys2);
        foreach (var (k1, k2) in keys1.Zip(keys2))
        {
            k1.KeyType.Should().Be(k2.KeyType);
            k1.KeyValue.Should().Be(k2.KeyValue);
        }
    }

    [Fact]
    public void ComputeKeys_DifferentNamesProducesDifferentKeys()
    {
        var request1 = CreateRequest(firstName: "Иван");
        var request2 = CreateRequest(firstName: "Петр");

        var keys1 = _sut.ComputeKeys(request1);
        var keys2 = _sut.ComputeKeys(request2);

        var fioKey1 = keys1.Single(k => k.KeyType == "fio").KeyValue;
        var fioKey2 = keys2.Single(k => k.KeyType == "fio").KeyValue;

        fioKey1.Should().NotBe(fioKey2);
    }

    [Fact]
    public void ComputeKeys_InvalidInn_SkipsInnKeys()
    {
        var request = CreateRequest(evidence: new Evidence { Inn = "123456789012" });

        var keys = _sut.ComputeKeys(request);

        keys.Should().NotContain(k => k.KeyType == "inn");
        keys.Should().NotContain(k => k.KeyType == "inn_fio");
    }

    [Fact]
    public void ComputeKeys_InvalidSnils_SkipsSnilsKeys()
    {
        var request = CreateRequest(evidence: new Evidence { Snils = "12345678901" });

        var keys = _sut.ComputeKeys(request);

        keys.Should().NotContain(k => k.KeyType == "snils");
        keys.Should().NotContain(k => k.KeyType == "snils_fio");
    }

    private static ResolveRequest CreateRequest(
        string firstName = "ИВАН",
        string lastName = "ИВАНОВ",
        string? middleName = "ИВАНОВИЧ",
        Evidence? evidence = null)
    {
        return new ResolveRequest
        {
            FirstName = firstName,
            LastName = lastName,
            MiddleName = middleName,
            Evidence = evidence,
            SourceSystemId = "TEST",
            ExternalPersonId = "ext-001"
        };
    }
}
