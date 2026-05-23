using DormitoryManagement.Application.Abstractions.Auth;
using DormitoryManagement.Application.Abstractions.Data;
using DormitoryManagement.Application.Abstractions.Services;
using DormitoryManagement.Application.DTOs.Audit;
using DormitoryManagement.Application.DTOs.Auth;
using DormitoryManagement.Application.Security;
using DormitoryManagement.Domain.Common;
using DormitoryManagement.Application.Abstractions.Repositories;
using DormitoryManagement.Domain.Entities;

namespace DormitoryManagement.Application.Tests;

internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = new();

    public int SaveChangesCount { get; private set; }
    public InMemoryTransaction? LastTransaction { get; private set; }

    public InMemoryRepository<TEntity> Set<TEntity>() where TEntity : BaseEntity
    {
        var type = typeof(TEntity);
        if (!_repositories.TryGetValue(type, out var repository))
        {
            repository = new InMemoryRepository<TEntity>();
            _repositories[type] = repository;
        }

        return (InMemoryRepository<TEntity>)repository;
    }

    public IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity => Set<TEntity>();

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCount++;
        return Task.FromResult(1);
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        LastTransaction = new InMemoryTransaction();
        return Task.FromResult<IUnitOfWorkTransaction>(LastTransaction);
    }
}

internal sealed class InMemoryRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    public List<TEntity> Items { get; } = new();

    public IQueryable<TEntity> Query() => Items.AsQueryable();
    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));
    public Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TEntity>>(Items);

    public Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TEntity entity)
    {
    }

    public void Remove(TEntity entity) => Items.Remove(entity);
}

internal sealed class InMemoryTransaction : IUnitOfWorkTransaction
{
    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task CommitAsync(CancellationToken ct = default)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        RolledBack = true;
        return Task.CompletedTask;
    }
}

internal sealed class AllowAllPermissionService : IPermissionService
{
    public Task<bool> HasPermissionAsync(string permissionName, CancellationToken ct = default) => Task.FromResult(true);
    public Task<AuthorizationResult> AuthorizeAsync(string permissionName, CancellationToken ct = default) => Task.FromResult(AuthorizationResult.Success());
    public Task EnsurePermissionAsync(string permissionName, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class RecordingAuditLogService : IAuditLogService
{
    public List<(string Action, string EntityName, Guid? EntityId, string? Details)> Entries { get; } = new();

    public Task WriteAsync(string action, string entityName, Guid? entityId = null, string? details = null, CancellationToken ct = default)
    {
        Entries.Add((action, entityName, entityId, details));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(string? searchText = null, int take = 100, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuditLogDto>>(Array.Empty<AuditLogDto>());
}

internal sealed class TestCurrentUser : ICurrentUserService
{
    public TestCurrentUser(string roleName, Guid? userId = null, Guid? studentId = null, Guid? managerId = null, Guid? buildingId = null)
    {
        CurrentUser = new CurrentUserDto
        {
            UserId = userId ?? Guid.NewGuid(),
            Username = roleName.ToLowerInvariant(),
            Email = roleName.ToLowerInvariant() + "@ktx.local",
            FullName = roleName,
            RoleName = roleName,
            StudentId = studentId,
            ManagerId = managerId,
            BuildingId = buildingId
        };
    }

    public CurrentUserDto? CurrentUser { get; }
    public Guid? UserId => CurrentUser?.UserId;
    public string? UserName => CurrentUser?.Username;
    public string? Email => CurrentUser?.Email;
    public string? FullName => CurrentUser?.FullName;
    public IReadOnlyCollection<string> Roles => CurrentUser?.RoleName is { Length: > 0 } roleName ? new[] { roleName } : Array.Empty<string>();
    public bool IsAuthenticated => CurrentUser is not null;
    public bool IsInRole(string roleName) => Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
}

internal sealed class TestPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"HASH::{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password))}";
    public bool VerifyPassword(string passwordHash, string password) => passwordHash == HashPassword(password);
}

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly InMemoryUnitOfWork _unitOfWork;

    public InMemoryUserRepository(InMemoryUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_unitOfWork.Set<User>().Items.FirstOrDefault(user => user.Id == id));

    public Task<User?> GetByEmailOrUsernameAsync(string value, CancellationToken ct = default)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return Task.FromResult(_unitOfWork.Set<User>().Items.FirstOrDefault(user =>
            user.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            user.Username.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult(_unitOfWork.Set<User>().Items.FirstOrDefault(user =>
            user.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return Task.FromResult(_unitOfWork.Set<User>().Items.FirstOrDefault(user =>
            user.Username.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<User?> GetByStudentCodeAsync(string studentCode, CancellationToken ct = default)
    {
        var normalized = studentCode.Trim().ToUpperInvariant();
        return Task.FromResult(_unitOfWork.Set<User>().Items.FirstOrDefault(user =>
            user.Student?.StudentCode.Equals(normalized, StringComparison.OrdinalIgnoreCase) == true));
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _unitOfWork.Set<User>().Items.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class InMemoryStudentRepository : IStudentRepository
{
    private readonly InMemoryUnitOfWork _unitOfWork;

    public InMemoryStudentRepository(InMemoryUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public IQueryable<Student> Query() => _unitOfWork.Set<Student>().Items.AsQueryable();
    public Task<Student?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_unitOfWork.Set<Student>().Items.FirstOrDefault(student => student.Id == id));

    public Task<Student?> GetByStudentCodeAsync(string studentCode, CancellationToken ct = default)
    {
        var normalized = studentCode.Trim().ToUpperInvariant();
        return Task.FromResult(_unitOfWork.Set<Student>().Items.FirstOrDefault(student =>
            student.StudentCode.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AddAsync(Student student, CancellationToken ct = default)
    {
        _unitOfWork.Set<Student>().Items.Add(student);
        return Task.CompletedTask;
    }

    public void Update(Student student)
    {
    }
}

internal sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly InMemoryUnitOfWork _unitOfWork;

    public InMemoryRoomRepository(InMemoryUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public IQueryable<Room> Query() => _unitOfWork.Set<Room>().Items.AsQueryable();
    public Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_unitOfWork.Set<Room>().Items.FirstOrDefault(room => room.Id == id));

    public Task AddAsync(Room room, CancellationToken ct = default)
    {
        _unitOfWork.Set<Room>().Items.Add(room);
        return Task.CompletedTask;
    }

    public void Update(Room room)
    {
    }
}

internal sealed class InMemoryPendingAccountRegistrationRepository : IPendingAccountRegistrationRepository
{
    private readonly InMemoryUnitOfWork _unitOfWork;

    public InMemoryPendingAccountRegistrationRepository(InMemoryUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public List<PendingAccountRegistration> Items => _unitOfWork.Set<PendingAccountRegistration>().Items;
    public IQueryable<PendingAccountRegistration> Query() => Items.AsQueryable();
    public Task<PendingAccountRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(registration => registration.Id == id));

    public Task<PendingAccountRegistration?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
        GetByIdAsync(id, ct);

    public Task AddAsync(PendingAccountRegistration registration, CancellationToken ct = default)
    {
        Items.Add(registration);
        return Task.CompletedTask;
    }

    public void Update(PendingAccountRegistration registration)
    {
    }

    public void Remove(PendingAccountRegistration registration) => Items.Remove(registration);

    public void RemoveRange(IEnumerable<PendingAccountRegistration> registrations)
    {
        foreach (var registration in registrations.ToArray())
        {
            Items.Remove(registration);
        }
    }
}
