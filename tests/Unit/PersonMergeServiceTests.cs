using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Mnemonios.Domain.Entities;
using Mnemonios.Infrastructure.Persistence;
using Mnemonios.Infrastructure.Services;
using Moq;
using Xunit;

namespace Mnemonios.UnitTests;

public class PersonMergeServiceTests : IDisposable
{
    private readonly Mock<ILogger<PersonMergeService>> _loggerMock;
    private readonly PersonMergeService _sut;
    private readonly AppDbContext _dbContext;

    private readonly Guid _survivingId = Guid.NewGuid();
    private readonly Guid _mergedId = Guid.NewGuid();

    public PersonMergeServiceTests()
    {
        _loggerMock = new Mock<ILogger<PersonMergeService>>();

        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new AppDbContext(dbContextOptions);

        _sut = new PersonMergeService(_dbContext, _loggerMock.Object);

        // Создать surviving-запись
        _dbContext.Persons.Add(new Person { MasterId = _survivingId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Persons.Add(new Person { MasterId = _mergedId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // =========================================================================
    // 1. Merge — слияние без дубликатов
    // =========================================================================

    [Fact]
    public async Task MergePersonsAsync_NoDuplicates_MovesAllKeysAndRemovesMerged()
    {
        // Arrange: surviving имеет 1 ключ, merged имеет 1 другой ключ
        var survivingKey = CreateKey(_survivingId, "inn", "hash_inn");
        var mergedKey = CreateKey(_mergedId, "snils", "hash_snils");
        _dbContext.PersonIdentificationKeys.AddRange(survivingKey, mergedKey);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.MergePersonsAsync(_survivingId, _mergedId, "test");

        // Assert: surviving теперь имеет 2 ключа
        var keys = await _dbContext.PersonIdentificationKeys.Where(k => k.MasterId == _survivingId).ToListAsync();
        keys.Should().HaveCount(2);
        keys.Should().Contain(k => k.KeyType == "inn");
        keys.Should().Contain(k => k.KeyType == "snils");

        // merged удалён
        var mergedKeys = await _dbContext.PersonIdentificationKeys.Where(k => k.MasterId == _mergedId).ToListAsync();
        mergedKeys.Should().BeEmpty();

        // merged-запись удалена
        var mergedPerson = await _dbContext.Persons.FindAsync(_mergedId);
        mergedPerson.Should().BeNull();

        // surviving на месте
        var survivingPerson = await _dbContext.Persons.FindAsync(_survivingId);
        survivingPerson.Should().NotBeNull();
    }

    // =========================================================================
    // 2. Merge — дубликаты ключей удаляются
    // =========================================================================

    [Fact]
    public async Task MergePersonsAsync_DuplicateKeys_RemovesDuplicatesKeepsUnique()
    {
        // Arrange: оба имеют одинаковый ключ "inn"
        var survivingKey = CreateKey(_survivingId, "inn", "same_hash");
        var mergedKey = CreateKey(_mergedId, "inn", "same_hash");
        var mergedUniqueKey = CreateKey(_mergedId, "dul", "hash_dul");
        _dbContext.PersonIdentificationKeys.AddRange(survivingKey, mergedKey, mergedUniqueKey);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.MergePersonsAsync(_survivingId, _mergedId, "test");

        // Assert: surviving имеет 2 ключа (inn + dul), дубликат inn удалён
        var keys = await _dbContext.PersonIdentificationKeys.Where(k => k.MasterId == _survivingId).ToListAsync();
        keys.Should().HaveCount(2);
        keys.Should().Contain(k => k.KeyType == "inn");
        keys.Should().Contain(k => k.KeyType == "dul");
    }

    // =========================================================================
    // 3. Merge — внешние ссылки переносятся
    // =========================================================================

    [Fact]
    public async Task MergePersonsAsync_ExternalIds_MovesToSurviving()
    {
        var extId = new PersonExternalId
        {
            Id = Guid.NewGuid(),
            MasterId = _mergedId,
            SourceSystemId = "CRM",
            ExternalPersonId = "emp-001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.PersonExternalIds.Add(extId);
        await _dbContext.SaveChangesAsync();

        await _sut.MergePersonsAsync(_survivingId, _mergedId, "test");

        var moved = await _dbContext.PersonExternalIds.Where(e => e.MasterId == _survivingId).ToListAsync();
        moved.Should().HaveCount(1);
        moved[0].SourceSystemId.Should().Be("CRM");
        moved[0].ExternalPersonId.Should().Be("emp-001");
    }

    // =========================================================================
    // 4. Merge — документы переносятся без дубликатов
    // =========================================================================

    [Fact]
    public async Task MergePersonsAsync_Documents_MovesUniqueRemovesDuplicates()
    {
        var survivingDoc = new PersonDocument
        {
            Id = Guid.NewGuid(),
            MasterId = _survivingId,
            DocumentType = "21",
            DocumentHash = "hash_a",
            CreatedAt = DateTime.UtcNow
        };
        var mergedDocDuplicate = new PersonDocument
        {
            Id = Guid.NewGuid(),
            MasterId = _mergedId,
            DocumentType = "21",
            DocumentHash = "hash_a", // дубликат
            CreatedAt = DateTime.UtcNow
        };
        var mergedDocUnique = new PersonDocument
        {
            Id = Guid.NewGuid(),
            MasterId = _mergedId,
            DocumentType = "10",
            DocumentHash = "hash_b", // уникальный
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.PersonDocuments.AddRange(survivingDoc, mergedDocDuplicate, mergedDocUnique);
        await _dbContext.SaveChangesAsync();

        await _sut.MergePersonsAsync(_survivingId, _mergedId, "test");

        var docs = await _dbContext.PersonDocuments.Where(d => d.MasterId == _survivingId).ToListAsync();
        docs.Should().HaveCount(2);
        docs.Should().Contain(d => d.DocumentHash == "hash_a");
        docs.Should().Contain(d => d.DocumentHash == "hash_b");
    }

    // =========================================================================
    // 5. Merge — дефекты merged удаляются
    // =========================================================================

    [Fact]
    public async Task MergePersonsAsync_Defects_RemovesFromMerged()
    {
        var defect = new PersonDefect
        {
            Id = Guid.NewGuid(),
            MasterId = _mergedId,
            DefectType = "invalid_inn",
            DefectMessage = "test",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.PersonDefects.Add(defect);
        await _dbContext.SaveChangesAsync();

        await _sut.MergePersonsAsync(_survivingId, _mergedId, "test");

        var defects = await _dbContext.PersonDefects.Where(d => d.MasterId == _mergedId).ToListAsync();
        defects.Should().BeEmpty();
    }

    // =========================================================================
    // 6. Merge — отложенные прекращения merged удаляются
    // =========================================================================

    [Fact]
    public async Task MergePersonsAsync_DeferredCessations_RemovesFromMerged()
    {
        var deferred = new PersonDeferredCessation
        {
            Id = Guid.NewGuid(),
            MasterId = _mergedId,
            SourceSystemId = "CRM",
            ExternalPersonId = "emp-001",
            ScheduledDeletionDate = DateTime.UtcNow.AddDays(30),
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.PersonDeferredCessations.Add(deferred);
        await _dbContext.SaveChangesAsync();

        await _sut.MergePersonsAsync(_survivingId, _mergedId, "test");

        var deferreds = await _dbContext.PersonDeferredCessations.Where(c => c.MasterId == _mergedId).ToListAsync();
        deferreds.Should().BeEmpty();
    }

    // =========================================================================
    // 7. Merge — нельзя слить с самой собой
    // =========================================================================

    [Fact]
    public async Task MergePersonsAsync_SameIds_ThrowsArgumentException()
    {
        var act = () => _sut.MergePersonsAsync(_survivingId, _survivingId, "test");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*самой собой*");
    }

    // =========================================================================
    // 8. Merge — логирование
    // =========================================================================

    [Fact]
    public async Task MergePersonsAsync_CallsLogWarning()
    {
        await _sut.MergePersonsAsync(_survivingId, _mergedId, "inn_match");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[Merge]")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static PersonIdentificationKey CreateKey(Guid masterId, string keyType, string keyValue)
    {
        return new PersonIdentificationKey
        {
            Id = Guid.NewGuid(),
            MasterId = masterId,
            KeyType = keyType,
            KeyValue = keyValue,
            NormalizationVersion = 1,
            CreatedAt = DateTime.UtcNow
        };
    }
}
