using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Entities;
using Mnemonios.Domain.Enums;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Persistence;
using Mnemonios.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Mnemonios.UnitTests;

public class PersonResolveServiceTests
{
    private const string TestHmacKey = "test_secret_key_for_hmac_computation_32chars!";

    private readonly Mock<IPersonRepository> _repositoryMock;
    private readonly Mock<IPersonCessationService> _cessationServiceMock;
    private readonly Mock<IPersonMergeService> _mergeServiceMock;
    private readonly PersonResolveService _sut;

    public PersonResolveServiceTests()
    {
        _repositoryMock = new Mock<IPersonRepository>();
        _cessationServiceMock = new Mock<IPersonCessationService>();
        _mergeServiceMock = new Mock<IPersonMergeService>();
        var normalizationService = new NormalizationService();
        var hmacSettings = Options.Create(new HmacSettings { Key = TestHmacKey });
        var keyService = new IdentificationKeyService(hmacSettings, normalizationService);

        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var dbContext = new AppDbContext(dbContextOptions);

        _sut = new PersonResolveService(
            _repositoryMock.Object,
            normalizationService,
            keyService,
            _cessationServiceMock.Object,
            _mergeServiceMock.Object,
            dbContext);

        SetupStagingMocks();
    }

    private void SetupStagingMocks()
    {
        _repositoryMock
            .Setup(r => r.CreateExtPersonAsync(It.IsAny<ExtPerson>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtPerson e, CancellationToken _) => e);
        _repositoryMock
            .Setup(r => r.SaveExtDefectsAsync(It.IsAny<IEnumerable<ExtPersonDefect>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repositoryMock
            .Setup(r => r.MarkExtPersonProcessedAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ResolveAsync_NoMatch_CreatesNewPerson()
    {
        var request = CreateRequest();
        var createdPerson = new Person { MasterId = Guid.NewGuid() };
        _repositoryMock
            .Setup(r => r.FindPersonIdsByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Person>(), It.IsAny<IEnumerable<PersonIdentificationKey>>(), It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPerson);

        var result = await _sut.ResolveAsync(request);

        result.Status.Should().Be(PersonMatchStatus.Unmatched);
        result.MasterId.Should().NotBeNull();
        result.HasDefects.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_SingleMatch_ReturnsMatched()
    {
        var request = CreateRequest();
        var existingPersonId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.FindPersonIdsByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingPersonId]);
        _repositoryMock
            .Setup(r => r.TryUpdateExternalIdAsync(It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (Guid?)null));
        _repositoryMock
            .Setup(r => r.AddExternalIdAsync(It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Добавить персону и ключи в InMemory БД
        var normalizationService = new NormalizationService();
        var hmacSettings = Options.Create(new HmacSettings { Key = TestHmacKey });
        var keyService = new IdentificationKeyService(hmacSettings, normalizationService);
        var keys = keyService.ComputeKeys(request, 1);

        var dbContext = CreateDbContext();

        // Добавить персону (нужна для FK)
        dbContext.Persons.Add(new Person
        {
            MasterId = existingPersonId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        // Добавить ключи
        foreach (var key in keys)
        {
            dbContext.PersonIdentificationKeys.Add(new PersonIdentificationKey
            {
                Id = Guid.NewGuid(),
                MasterId = existingPersonId,
                KeyType = key.KeyType,
                KeyValue = key.KeyValue,
                NormalizationVersion = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        // Создать _sut с тем же контекстом
        var sut = new PersonResolveService(
            _repositoryMock.Object,
            normalizationService,
            keyService,
            _cessationServiceMock.Object,
            _mergeServiceMock.Object,
            dbContext);

        var result = await sut.ResolveAsync(request);

        result.Status.Should().Be(PersonMatchStatus.Matched);
        result.MasterId.Should().Be(existingPersonId);
        result.HasDefects.Should().BeFalse();
        _repositoryMock.Verify(r =>
            r.CreateAsync(It.IsAny<Person>(), It.IsAny<IEnumerable<PersonIdentificationKey>>(), It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_MultipleMatches_SelectsBestMatch()
    {
        var request = CreateRequest();
        var personId1 = Guid.NewGuid();
        var personId2 = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.FindPersonIdsByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([personId1, personId2]);
        _repositoryMock
            .Setup(r => r.TryUpdateExternalIdAsync(It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (Guid?)null));
        _repositoryMock
            .Setup(r => r.AddExternalIdAsync(It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Добавить ключи для personId1 (совпадают с запросом)
        var normalizationService = new NormalizationService();
        var hmacSettings = Options.Create(new HmacSettings { Key = TestHmacKey });
        var keyService = new IdentificationKeyService(hmacSettings, normalizationService);
        var keys = keyService.ComputeKeys(request, 1);

        var dbContext = CreateDbContext();
        dbContext.Persons.Add(new Person { MasterId = personId1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        dbContext.Persons.Add(new Person { MasterId = personId2, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        foreach (var key in keys)
        {
            dbContext.PersonIdentificationKeys.Add(new PersonIdentificationKey
            {
                Id = Guid.NewGuid(),
                MasterId = personId1,
                KeyType = key.KeyType,
                KeyValue = key.KeyValue,
                NormalizationVersion = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        var sut = new PersonResolveService(
            _repositoryMock.Object,
            normalizationService,
            keyService,
            _cessationServiceMock.Object,
            _mergeServiceMock.Object,
            dbContext);

        var result = await sut.ResolveAsync(request);

        // Выбирается кандидат с наибольшим совпадением (personId1)
        result.Status.Should().Be(PersonMatchStatus.Matched);
        result.MasterId.Should().Be(personId1);
    }

    [Fact]
    public async Task ResolveAsync_InvalidRequest_ThrowsArgumentException()
    {
        var request = CreateRequest(firstName: "");

        var act = () => _sut.ResolveAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveAsync_Matched_TriesToUpdateExternalIdFirst()
    {
        var request = CreateRequest();
        var existingPersonId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.FindPersonIdsByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingPersonId]);
        _repositoryMock
            .Setup(r => r.TryUpdateExternalIdAsync(It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (Guid?)Guid.NewGuid()));

        // Добавить ключи в InMemory БД
        var normalizationService = new NormalizationService();
        var hmacSettings = Options.Create(new HmacSettings { Key = TestHmacKey });
        var keyService = new IdentificationKeyService(hmacSettings, normalizationService);
        var keys = keyService.ComputeKeys(request, 1);

        var dbContext = CreateDbContext();
        dbContext.Persons.Add(new Person { MasterId = existingPersonId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        foreach (var key in keys)
        {
            dbContext.PersonIdentificationKeys.Add(new PersonIdentificationKey
            {
                Id = Guid.NewGuid(),
                MasterId = existingPersonId,
                KeyType = key.KeyType,
                KeyValue = key.KeyValue,
                NormalizationVersion = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        var sut = new PersonResolveService(
            _repositoryMock.Object,
            normalizationService,
            keyService,
            _cessationServiceMock.Object,
            _mergeServiceMock.Object,
            dbContext);

        await sut.ResolveAsync(request);

        _repositoryMock.Verify(r =>
            r.TryUpdateExternalIdAsync(It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(r =>
            r.AddExternalIdAsync(It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_InvalidInn_CreatesPersonWithDefect()
    {
        var request = CreateRequest(evidence: new Evidence { Inn = "123456789012" });
        var createdPerson = new Person { MasterId = Guid.NewGuid() };

        _repositoryMock
            .Setup(r => r.FindPersonIdsByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Person>(), It.IsAny<IEnumerable<PersonIdentificationKey>>(), It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPerson);
        _repositoryMock
            .Setup(r => r.SaveDefectsAsync(It.IsAny<IEnumerable<PersonDefect>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ResolveAsync(request);

        result.Status.Should().Be(PersonMatchStatus.Unmatched);
        result.HasDefects.Should().BeTrue();
        result.Defects.Should().Contain(d => d.DefectType == "invalid_inn");
        _repositoryMock.Verify(r =>
            r.SaveDefectsAsync(It.IsAny<IEnumerable<PersonDefect>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_InvalidSnils_CreatesPersonWithDefect()
    {
        var request = CreateRequest(evidence: new Evidence { Snils = "12345678901" });
        var createdPerson = new Person { MasterId = Guid.NewGuid() };

        _repositoryMock
            .Setup(r => r.FindPersonIdsByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Person>(), It.IsAny<IEnumerable<PersonIdentificationKey>>(), It.IsAny<PersonExternalId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPerson);
        _repositoryMock
            .Setup(r => r.SaveDefectsAsync(It.IsAny<IEnumerable<PersonDefect>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ResolveAsync(request);

        result.Status.Should().Be(PersonMatchStatus.Unmatched);
        result.HasDefects.Should().BeTrue();
        result.Defects.Should().Contain(d => d.DefectType == "invalid_snils");
    }

    [Fact]
    public async Task ResolveAsync_MissingSourceSystemId_ThrowsException()
    {
        var request = new ResolveRequest
        {
            FirstName = "Иван",
            LastName = "Иванов",
            SourceSystemId = "",
            ExternalPersonId = "ext-12345"
        };

        var act = () => _sut.ResolveAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveAsync_MissingLastName_ThrowsException()
    {
        var request = CreateRequest(lastName: "");

        var act = () => _sut.ResolveAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }



    private static ResolveRequest CreateRequest(
        string firstName = "Иван",
        string lastName = "Иванов",
        Evidence? evidence = null)
    {
        return new ResolveRequest
        {
            FirstName = firstName,
            LastName = lastName,
            SourceSystemId = "CRM",
            ExternalPersonId = "ext-12345",
            Evidence = evidence ?? new Evidence { DulType = "21", DulSeries = "4510", DulNumber = "123456" }
        };
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }
}
