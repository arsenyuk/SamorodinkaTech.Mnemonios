using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Entities;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Persistence;
using Mnemonios.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Mnemonios.UnitTests;

public class PersonCessationServiceTests
{
    private readonly Mock<IPersonRepository> _repositoryMock;
    private readonly Mock<ILogger<PersonCessationService>> _loggerMock;
    private readonly Mock<IClientIpProvider> _ipProviderMock;
    private readonly PersonCessationService _sut;
    private readonly AppDbContext _dbContext;

    public PersonCessationServiceTests()
    {
        _repositoryMock = new Mock<IPersonRepository>();
        _loggerMock = new Mock<ILogger<PersonCessationService>>();
        _ipProviderMock = new Mock<IClientIpProvider>();
        _ipProviderMock.Setup(p => p.GetClientIp()).Returns("127.0.0.1");

        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new AppDbContext(dbContextOptions);

        _sut = new PersonCessationService(_repositoryMock.Object, _loggerMock.Object, _ipProviderMock.Object, _dbContext);

        SetupStagingMocks();
    }

    private void SetupStagingMocks()
    {
        _repositoryMock
            .Setup(r => r.CreateExtCessationAsync(It.IsAny<ExtPersonCessation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtPersonCessation e, CancellationToken _) => e);
        _repositoryMock
            .Setup(r => r.CreateExtDeferredCessationAsync(It.IsAny<ExtPersonDeferredCessation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtPersonDeferredCessation e, CancellationToken _) => e);
    }

    // =========================================================================
    // Phase 1: CeaseProcessingAsync — only marks, doesn't delete
    // =========================================================================

    [Fact]
    public async Task CeaseProcessingAsync_PersonFound_MarksCessation()
    {
        var personId = Guid.NewGuid();
        var extPerson = new ExtPerson
        {
            Id = Guid.NewGuid(),
            MasterId = personId,
            SourceSystemId = "CRM",
            ExternalPersonId = "ext-001",
            CreatedAt = DateTime.UtcNow
        };

        SetupMocksForMarking(personId, extPerson);

        var result = await _sut.CeaseProcessingAsync(
            new CessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = "ext-001" }], OrganizationUnitKey = "" });

        result.Should().NotBeNull();
        result!.MasterId.Should().Be(personId);
        result.DeletedKeys.Should().Be(0);
        result.DeletedExternalIds.Should().Be(0);
        result.DeletedDefects.Should().Be(0);

        _repositoryMock.Verify(r =>
            r.CreateExtCessationAsync(It.IsAny<ExtPersonCessation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CeaseProcessingAsync_PersonNotFound_ReturnsNull()
    {
        _repositoryMock
            .Setup(r => r.FindMasterIdByExternalIdAsync("CRM", "ext-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var result = await _sut.CeaseProcessingAsync(
            new CessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = "ext-999" }], OrganizationUnitKey = "" });

        result.Should().BeNull();

        _repositoryMock.Verify(r =>
            r.CreateExtCessationAsync(It.IsAny<ExtPersonCessation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CeaseProcessingAsync_EmptyIdentifier_ReturnsNull()
    {
        var request = new CessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = "" }], OrganizationUnitKey = "" };

        var result = await _sut.CeaseProcessingAsync(request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CeaseProcessingAsync_NoIdentifiersNoOrgKey_ThrowsArgumentException()
    {
        var request = new CessationRequest { };

        var act = () => _sut.CeaseProcessingAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CeaseProcessingAsync_BothIdentifiersAndOrgKey_ThrowsArgumentException()
    {
        var request = new CessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = "ext-001" }],
            OrganizationUnitKey = "ORG-001"
        };

        var act = () => _sut.CeaseProcessingAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*одновременно*");
    }

    // =========================================================================
    // Deferred cessation tests
    // =========================================================================

    [Fact]
    public async Task DeferProcessingAsync_PersonFound_CreatesRecord()
    {
        var personId = Guid.NewGuid();
        var futureDate = DateTime.UtcNow.AddDays(30);
        var extPerson = new ExtPerson
        {
            Id = Guid.NewGuid(),
            MasterId = personId,
            SourceSystemId = "CRM",
            ExternalPersonId = "ext-001",
            CreatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.FindMasterIdByExternalIdAsync("CRM", "ext-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(personId);
        _repositoryMock
            .Setup(r => r.GetPendingDeferredCessationAsync("CRM", "ext-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonDeferredCessation?)null);
        _repositoryMock
            .Setup(r => r.AddDeferredCessationAsync(It.IsAny<PersonDeferredCessation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Seed ext_person for FK reference
        _dbContext.ExtPersons.Add(extPerson);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.DeferProcessingAsync(
            new DeferredCessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = "ext-001" }], ScheduledDeletionDate = futureDate, OrganizationUnitKey = "" });

        result.Should().NotBeNull();
        result!.MasterId.Should().Be(personId);
        result.ScheduledDeletionDate.Should().Be(futureDate);

        _repositoryMock.Verify(r =>
            r.AddDeferredCessationAsync(It.IsAny<PersonDeferredCessation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeferProcessingAsync_PersonNotFound_ReturnsResponseWithNullPersonId()
    {
        _repositoryMock
            .Setup(r => r.FindMasterIdByExternalIdAsync("CRM", "ext-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var result = await _sut.DeferProcessingAsync(
            new DeferredCessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = "ext-999" }], ScheduledDeletionDate = DateTime.UtcNow.AddDays(30), OrganizationUnitKey = "" });

        result.Should().NotBeNull();
        result!.MasterId.Should().BeNull();
    }

    [Fact]
    public async Task DeferProcessingAsync_PastDate_ThrowsArgumentException()
    {
        var request = new DeferredCessationRequest { Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = "ext-001" }], ScheduledDeletionDate = DateTime.UtcNow.AddDays(-1), OrganizationUnitKey = "" };

        var act = () => _sut.DeferProcessingAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeferProcessingAsync_AlreadyPending_SkipsRecord()
    {
        var personId = Guid.NewGuid();
        var futureDate = DateTime.UtcNow.AddDays(30);

        _repositoryMock
            .Setup(r => r.FindMasterIdByExternalIdAsync("CRM", "ext-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(personId);
        _repositoryMock
            .Setup(r => r.GetPendingDeferredCessationAsync("CRM", "ext-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonDeferredCessation
            {
                Id = Guid.NewGuid(),
                MasterId = personId,
                SourceSystemId = "CRM",
                ExternalPersonId = "ext-001",
                ScheduledDeletionDate = futureDate,
                Status = "pending"
            });

        var result = await _sut.DeferProcessingAsync(
            new DeferredCessationRequest
            {
                Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = "ext-001" }],
                ScheduledDeletionDate = futureDate.AddDays(10),
                OrganizationUnitKey = ""
            });

        result.Should().NotBeNull();
        result!.MasterId.Should().Be(personId);

        _repositoryMock.Verify(r =>
            r.AddDeferredCessationAsync(It.IsAny<PersonDeferredCessation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeferProcessingAsync_NoIdentifiersNoOrgKey_ThrowsArgumentException()
    {
        var request = new DeferredCessationRequest
        {
            ScheduledDeletionDate = DateTime.UtcNow.AddDays(30)
        };

        var act = () => _sut.DeferProcessingAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeferProcessingAsync_BothIdentifiersAndOrgKey_ThrowsArgumentException()
    {
        var request = new DeferredCessationRequest
        {
            Identifiers = [new CessationIdentifierDto { SourceSystemId = "CRM", ExternalPersonId = "ext-001" }],
            ScheduledDeletionDate = DateTime.UtcNow.AddDays(30),
            OrganizationUnitKey = "ORG-001"
        };

        var act = () => _sut.DeferProcessingAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*одновременно*");
    }

    [Fact]
    public async Task CancelDeferredCessationAsync_PendingFound_CancelsRecord()
    {
        var pending = new PersonDeferredCessation
        {
            Id = Guid.NewGuid(),
            MasterId = Guid.NewGuid(),
            SourceSystemId = "CRM",
            ExternalPersonId = "ext-001",
            ScheduledDeletionDate = DateTime.UtcNow.AddDays(30),
            Status = "pending"
        };

        _repositoryMock
            .Setup(r => r.GetPendingDeferredCessationAsync("CRM", "ext-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        _repositoryMock
            .Setup(r => r.CancelDeferredCessationRecordAsync(pending, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.CancelDeferredCessationAsync("CRM", "ext-001");

        _repositoryMock.Verify(r =>
            r.CancelDeferredCessationRecordAsync(pending, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelDeferredCessationAsync_NoPending_DoesNothing()
    {
        _repositoryMock
            .Setup(r => r.GetPendingDeferredCessationAsync("CRM", "ext-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonDeferredCessation?)null);

        await _sut.CancelDeferredCessationAsync("CRM", "ext-999");

        _repositoryMock.Verify(r =>
            r.CancelDeferredCessationRecordAsync(It.IsAny<PersonDeferredCessation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void SetupMocksForMarking(Guid personId, ExtPerson extPerson)
    {
        _repositoryMock
            .Setup(r => r.FindMasterIdByExternalIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(personId);

        // Seed ext_person for FK reference
        _dbContext.ExtPersons.Add(extPerson);
        _dbContext.SaveChanges();
    }
}
