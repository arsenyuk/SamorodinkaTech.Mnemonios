# Contributing to Mnemonios

Thank you for your interest in contributing to Mnemonios! This document provides guidelines and information for contributors.

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Process](#development-process)
- [Coding Standards](#coding-standards)
- [Commit Guidelines](#commit-guidelines)
- [Pull Request Process](#pull-request-process)
- [Testing Requirements](#testing-requirements)
- [Documentation](#documentation)
- [Reporting Issues](#reporting-issues)

---

## Code of Conduct

We are committed to providing a welcoming and inclusive experience for everyone. Please be respectful and constructive in all interactions.

---

## Getting Started

### Prerequisites

- .NET 10 SDK
- Docker & Docker Compose
- Git
- IDE (Visual Studio 2022, JetBrains Rider, or VS Code)

### Setup

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/your-username/mnemonios.git
   cd mnemonios
   ```
3. Add upstream remote:
   ```bash
   git remote add upstream https://github.com/samorodinkatech/mnemonios.git
   ```
4. Create a branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
5. Start development environment:
   ```bash
   docker-compose up -d
   dotnet run --project src/Api
   ```

---

## Development Process

### Workflow

1. **Pick an issue**: Look at [GitHub Issues](https://github.com/samorodinkatech/mnemonios/issues) for tasks
2. **Create a branch**: Use the naming convention below
3. **Write code**: Follow coding standards
4. **Write tests**: Maintain or improve coverage
5. **Submit PR**: Follow PR template
6. **Code review**: Address feedback
7. **Merge**: After approval

### Branch Naming

| Prefix | Description | Example |
|--------|-------------|---------|
| `feature/` | New feature | `feature/user-auth` |
| `bugfix/` | Bug fix | `bugfix/login-error` |
| `hotfix/` | Critical fix | `hotfix/security-patch` |
| `docs/` | Documentation | `docs/api-update` |
| `refactor/` | Code refactoring | `refactor/clean-architecture` |
| `test/` | Tests | `test/unit-tests-verification` |

---

## Coding Standards

### C# Style

```csharp
// ✅ Correct
public class VerificationService : IVerificationService
{
    private readonly IVerificationRepository _repository;
    private readonly ILogger<VerificationService> _logger;

    public VerificationService(
        IVerificationRepository repository,
        ILogger<VerificationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new verification request.
    /// </summary>
    /// <param name="request">The request data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created request ID.</returns>
    public async Task<Guid> CreateAsync(
        VerificationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = VerificationMapper.ToEntity(request);
        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }
}
```

```csharp
// ❌ Incorrect
public class BadExample
{
    VerificationRepository repo;
    public BadExample(VerificationRepository r) { repo = r; }
    public async Task<Guid> Create(VerificationRequestDto req)
    {
        var e = new VerificationRequest();
        e.Id = Guid.NewGuid();
        e.ClientId = req.ClientId;
        e.Status = VerificationStatus.Pending;
        await repo.Add(e);
        return e.Id;
    }
}
```

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `VerificationService` |
| Interfaces | I + PascalCase | `IVerificationService` |
| Methods | PascalCase | `CreateAsync` |
| Properties | PascalCase | `ClientId` |
| Parameters | camelCase | `cancellationToken` |
| Local variables | camelCase | `verificationRequest` |
| Constants | PascalCase | `MaxRetryCount` |
| Private fields | _camelCase | `_repository` |

### Code Organization

```
src/
├── Domain/                 # Entities, Value Objects, Events, Interfaces
├── Infrastructure/         # Repositories, External Services, Persistence
├── Common/                 # Shared Utilities
└── Api/                    # Controllers, Middleware, Filters
```

### SOLID Principles

- **S**ingle Responsibility: One class = one responsibility
- **O**pen/Closed: Open for extension, closed for modification
- **L**iskov Substitution: Subtypes must be substitutable
- **I**nterface Segregation: Many specific interfaces
- **D**ependency Inversion: Depend on abstractions

---

## Commit Guidelines

### Commit Message Format

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Types

| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation |
| `style` | Formatting (no logic change) |
| `refactor` | Code refactoring |
| `test` | Adding tests |
| `chore` | Build, tooling |
| `perf` | Performance improvement |
| `ci` | CI/CD changes |

### Examples

```bash
# Feature
git commit -m "feat(auth): add JWT token refresh"

# Bug fix
git commit -m "fix(api): correct response format"

# Documentation
git commit -m "docs(readme): update installation instructions"

# Breaking change
git commit -m "feat(api)!: change response format

BREAKING CHANGE: response now includes 'result' field instead of 'status'"
```

### Rules

- Use imperative mood ("add feature" not "added feature")
- Keep first line under 72 characters
- Reference issues with #issue-number
- Don't commit generated files

---

## Pull Request Process

### Before Submitting

- [ ] Code compiles without errors
- [ ] All tests pass
- [ ] Code follows style guidelines
- [ ] Documentation updated (if needed)
- [ ] No merge conflicts with `develop`

### PR Template

```markdown
## Description

Brief description of changes.

## Type of Change

- [ ] Bug fix (non-breaking change fixing an issue)
- [ ] New feature (non-breaking change adding functionality)
- [ ] Breaking change (fix or feature causing existing functionality to change)
- [ ] Documentation update

## Testing

- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing performed

## Checklist

- [ ] My code follows the project's coding standards
- [ ] I have performed a self-review of my code
- [ ] I have commented my code where necessary
- [ ] I have updated documentation accordingly
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix/feature works
- [ ] New and existing unit tests pass locally

## Related Issues

Closes #123
```

### Review Process

1. **Automated checks**: CI/CD must pass
2. **Code review**: At least 1 approval required
3. **Address feedback**: Make requested changes
4. **Merge**: Squash merge to `develop`

---

## Testing Requirements

### Coverage Requirements

| Type | Minimum Coverage |
|------|------------------|
| Unit Tests | 80% |
| Integration Tests | 60% |
| Overall | 70% |

### Writing Tests

```csharp
// Unit Test Example
public class VerificationServiceTests
{
    private readonly Mock<IVerificationRepository> _repositoryMock;
    private readonly Mock<ILogger<VerificationService>> _loggerMock;
    private readonly VerificationService _sut;

    public VerificationServiceTests()
    {
        _repositoryMock = new Mock<IVerificationRepository>();
        _loggerMock = new Mock<ILogger<VerificationService>>();
        _sut = new VerificationService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WhenValidRequest_ShouldReturnNewId()
    {
        // Arrange
        var request = new VerificationRequestDto
        {
            ClientId = Guid.NewGuid(),
            RequestType = RequestType.FullCheck
        };

        _repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<VerificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerificationRequest { Id = Guid.NewGuid() });

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.Should().NotBeEmpty();
        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<VerificationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### Test Naming Convention

```csharp
[Fact]
public async Task MethodName_Condition_ExpectedResult()
{
    // Arrange
    // Act
    // Assert
}
```

### Running Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/Unit

# With coverage
dotnet test /p:CollectCoverage=true

# Filter
dotnet test --filter "FullyQualifiedName~VerificationServiceTests"
```

---

## Documentation

### When to Update

- Adding new features
- Changing public APIs
- Modifying configuration
- Updating dependencies

### Documentation Standards

- Use clear, concise language
- Include code examples
- Keep documentation up-to-date
- Follow existing style

### Documentation Files

| File | Purpose |
|------|---------|
| `README.md` | Project overview |
| `docs/architecture.md` | Technical architecture |
| `docs/decision_records/` | Architecture Decision Records |
| `CONTRIBUTING.md` | This file |

---

## Reporting Issues

### Bug Reports

Include:
- Steps to reproduce
- Expected behavior
- Actual behavior
- Environment details
- Screenshots (if applicable)

### Feature Requests

Include:
- Use case description
- Proposed solution
- Alternatives considered
- Additional context

---

## Questions?

- Create a discussion: [GitHub Discussions](https://github.com/samorodinkatech/mnemonios/discussions)
- Contact team: dev@samorodinkatech.ru

---

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
