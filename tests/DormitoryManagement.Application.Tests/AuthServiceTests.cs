using DormitoryManagement.Application.Abstractions.Auth;
using DormitoryManagement.Application.Abstractions.Data;
using DormitoryManagement.Application.Abstractions.Repositories;
using DormitoryManagement.Application.Abstractions.Services;
using DormitoryManagement.Application.Common;
using DormitoryManagement.Application.DTOs.Audit;
using DormitoryManagement.Application.DTOs.Auth;
using DormitoryManagement.Application.Services.Auth;
using DormitoryManagement.Domain.Common;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WithEmail_SucceedsAndSetsSession()
    {
        var fixture = AuthFixture.Create();

        var result = await fixture.Service.LoginAsync(new LoginRequest
        {
            EmailOrUsernameOrStudentCode = "admin@ktx.local",
            Password = "123456"
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal(RoleNames.Admin, result.User.RoleName);
        Assert.True(fixture.Session.IsAuthenticated);
        Assert.Equal(0, fixture.Admin.FailedLoginCount);
        Assert.NotNull(fixture.Admin.LastLoginAt);
    }

    [Fact]
    public async Task LoginAsync_WithUsername_Succeeds()
    {
        var fixture = AuthFixture.Create();

        var result = await fixture.Service.LoginAsync(new LoginRequest
        {
            EmailOrUsernameOrStudentCode = "admin",
            Password = "123456"
        });

        Assert.True(result.Succeeded);
        Assert.Equal("admin", result.User?.Username);
    }

    [Fact]
    public async Task LoginAsync_WithStudentCode_Succeeds()
    {
        var fixture = AuthFixture.Create();

        var result = await fixture.Service.LoginAsync(new LoginRequest
        {
            EmailOrUsernameOrStudentCode = "SV001",
            Password = "123456"
        });

        Assert.True(result.Succeeded);
        Assert.Equal(RoleNames.Student, result.User?.RoleName);
        Assert.Equal(fixture.Student.Id, result.User?.StudentId);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_IncrementsFailedLoginCount()
    {
        var fixture = AuthFixture.Create();

        var result = await fixture.Service.LoginAsync(new LoginRequest
        {
            EmailOrUsernameOrStudentCode = "admin@ktx.local",
            Password = "wrong1"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Tài khoản hoặc mật khẩu không đúng.", result.ErrorMessage);
        Assert.Equal(LoginFailureReason.InvalidPassword, result.FailureReason);
        Assert.Equal(1, fixture.Admin.FailedLoginCount);
        Assert.Equal(UserStatus.Active, fixture.Admin.Status);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownAccount_ReturnsAccountNotFound()
    {
        var fixture = AuthFixture.Create();

        var result = await fixture.Service.LoginAsync(new LoginRequest
        {
            EmailOrUsernameOrStudentCode = "missing@ktx.local",
            Password = "123456"
        });

        Assert.False(result.Succeeded);
        Assert.Equal(LoginFailureReason.AccountNotFound, result.FailureReason);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task LoginAsync_WhenFailedAttemptsReachLimit_LocksAccount()
    {
        var fixture = AuthFixture.Create(maxFailedAttempts: 2, lockoutMinutes: 5);
        fixture.Admin.FailedLoginCount = 1;

        var result = await fixture.Service.LoginAsync(new LoginRequest
        {
            EmailOrUsernameOrStudentCode = "admin@ktx.local",
            Password = "wrong1"
        });

        Assert.False(result.Succeeded);
        Assert.Equal(UserStatus.Locked, fixture.Admin.Status);
        Assert.NotNull(fixture.Admin.LockedUntil);
        Assert.True(fixture.Admin.LockedUntil > fixture.Clock.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_WithDisabledUser_Fails()
    {
        var fixture = AuthFixture.Create();
        fixture.Admin.Status = UserStatus.Disabled;

        var result = await fixture.Service.LoginAsync(new LoginRequest
        {
            EmailOrUsernameOrStudentCode = "admin@ktx.local",
            Password = "123456"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Tài khoản đã bị vô hiệu hóa.", result.ErrorMessage);
        Assert.Equal(LoginFailureReason.Disabled, result.FailureReason);
        Assert.False(fixture.Session.IsAuthenticated);
    }

    [Fact]
    public async Task LoginAsync_ForStudent_ReturnsOnlyStudentRole()
    {
        var fixture = AuthFixture.Create();

        var result = await fixture.Service.LoginAsync(new LoginRequest
        {
            EmailOrUsernameOrStudentCode = "student01@ktx.local",
            Password = "123456"
        });

        Assert.True(result.Succeeded);
        Assert.Equal(RoleNames.Student, result.User?.RoleName);
        Assert.DoesNotContain(RoleNames.Admin, fixture.Session.Roles);
    }

    [Fact]
    public void PasswordHash_DoesNotStorePlainText()
    {
        var hasher = new FakePasswordHasher();
        var hash = hasher.HashPassword("123456");

        Assert.NotEqual("123456", hash);
        Assert.DoesNotContain("123456", hash, StringComparison.Ordinal);
        Assert.True(hasher.VerifyPassword(hash, "123456"));
    }

    private sealed class AuthFixture
    {
        private AuthFixture(int maxFailedAttempts, int lockoutMinutes)
        {
            Hasher = new FakePasswordHasher();
            Clock = new FakeDateTimeProvider();
            Session = new FakeSessionService();
            UnitOfWork = new FakeUnitOfWork();
            AuditLog = new FakeAuditLogService();

            var adminRole = new Role { Id = Guid.NewGuid(), Name = RoleNames.Admin };
            var studentRole = new Role { Id = Guid.NewGuid(), Name = RoleNames.Student };
            Admin = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@ktx.local",
                FullName = "System Admin",
                PasswordHash = Hasher.HashPassword("123456"),
                Role = adminRole,
                RoleId = adminRole.Id,
                Status = UserStatus.Active
            };

            StudentUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "student01",
                Email = "student01@ktx.local",
                FullName = "Nguyen Van An",
                PasswordHash = Hasher.HashPassword("123456"),
                Role = studentRole,
                RoleId = studentRole.Id,
                Status = UserStatus.Active
            };

            Student = new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "SV001",
                FullName = StudentUser.FullName,
                UserId = StudentUser.Id,
                User = StudentUser
            };
            StudentUser.Student = Student;

            Repository = new FakeUserRepository(Admin, StudentUser);
            Service = new AuthService(
                Repository,
                Hasher,
                Session,
                Session,
                UnitOfWork,
                AuditLog,
                Clock,
                new SecurityOptions
                {
                    MaxFailedLoginAttempts = maxFailedAttempts,
                    LockoutMinutes = lockoutMinutes
                });
        }

        public AuthService Service { get; }
        public FakeUserRepository Repository { get; }
        public FakePasswordHasher Hasher { get; }
        public FakeSessionService Session { get; }
        public FakeUnitOfWork UnitOfWork { get; }
        public FakeAuditLogService AuditLog { get; }
        public FakeDateTimeProvider Clock { get; }
        public User Admin { get; }
        public User StudentUser { get; }
        public Student Student { get; }

        public static AuthFixture Create(int maxFailedAttempts = 5, int lockoutMinutes = 5) =>
            new(maxFailedAttempts, lockoutMinutes);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _users;

        public FakeUserRepository(params User[] users)
        {
            _users = users.ToList();
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(x => x.Id == id));

        public Task<User?> GetByEmailOrUsernameAsync(string value, CancellationToken ct = default)
        {
            var normalized = value.Trim().ToLowerInvariant();
            return Task.FromResult(_users.FirstOrDefault(x =>
                x.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                x.Username.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return Task.FromResult(_users.FirstOrDefault(x => x.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        {
            var normalized = username.Trim().ToLowerInvariant();
            return Task.FromResult(_users.FirstOrDefault(x => x.Username.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<User?> GetByStudentCodeAsync(string studentCode, CancellationToken ct = default)
        {
            var normalized = studentCode.Trim().ToLowerInvariant();
            return Task.FromResult(_users.FirstOrDefault(x =>
                x.Student?.StudentCode.Equals(normalized, StringComparison.OrdinalIgnoreCase) == true));
        }

        public Task AddAsync(User user, CancellationToken ct = default)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) =>
            $"HASHED::{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password))}";

        public bool VerifyPassword(string passwordHash, string password) => passwordHash == HashPassword(password);
    }

    private sealed class FakeSessionService : ISessionService, ICurrentUserService
    {
        public CurrentUserDto? CurrentUser { get; private set; }
        public Guid? UserId => CurrentUser?.UserId;
        public string? UserName => CurrentUser?.Username;
        public string? Email => CurrentUser?.Email;
        public string? FullName => CurrentUser?.FullName;
        public IReadOnlyCollection<string> Roles => CurrentUser?.RoleName is { Length: > 0 } roleName ? new[] { roleName } : Array.Empty<string>();
        public bool IsAuthenticated => CurrentUser is not null;
        public void SetCurrentUser(CurrentUserDto user) => CurrentUser = user;
        public void Clear() => CurrentUser = null;
        public bool IsInRole(string roleName) => Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
            Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());
    }

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAuditLogService : IAuditLogService
    {
        public Task WriteAsync(string action, string entityName, Guid? entityId = null, string? details = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(string? searchText = null, int take = 100, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AuditLogDto>>(Array.Empty<AuditLogDto>());
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = new(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc);
    }
}
