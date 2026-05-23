using DormitoryManagement.Application.Abstractions.Auth;
using DormitoryManagement.Application.Abstractions.Services;
using DormitoryManagement.Application.DTOs.Audit;
using DormitoryManagement.Application.DTOs.Auth;
using DormitoryManagement.Application.DTOs.Registrations;
using DormitoryManagement.Application.Security;
using DormitoryManagement.Application.Services.Registrations;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;
using DormitoryManagement.Infrastructure.Data;
using DormitoryManagement.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagement.Infrastructure.Tests;

public sealed class RoomRegistrationServiceEfTests
{
    [Fact]
    public async Task ApproveRegistrationAsync_creates_prepayment_invoice_with_relational_query_provider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DormitoryDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new DormitoryDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var building = new Building { Id = Guid.NewGuid(), Code = "T", Name = "Test Building" };
        var floor = new Floor { Id = Guid.NewGuid(), BuildingId = building.Id, FloorNumber = 1 };
        var room = new Room
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            FloorId = floor.Id,
            RoomNumber = "101",
            Capacity = 4,
            MonthlyPrice = 750000m,
            Status = RoomStatus.Available,
            GenderType = RoomGenderType.Male
        };
        var student = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SV999",
            FullName = "Test Student",
            Gender = "Male"
        };
        var registration = new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            RoomId = room.Id,
            Status = RegistrationStatus.Pending,
            ContractTermMonths = 6
        };
        dbContext.AddRange(
            building,
            floor,
            room,
            student,
            registration,
            new FeeType { Id = Guid.NewGuid(), Code = "room_fee", Name = "Room fee", IsRecurring = true });
        await dbContext.SaveChangesAsync();
        var service = new RoomRegistrationService(
            new AllowAllPermissionService(),
            new UnitOfWork(dbContext),
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Manager));

        await service.ApproveRegistrationAsync(new ApproveRoomRegistrationRequest
        {
            RegistrationId = registration.Id,
            StartDate = new DateTime(2026, 6, 1)
        });

        var invoice = await dbContext.Invoices.SingleAsync(invoice => invoice.StudentId == student.Id);
        Assert.Equal(InvoiceKind.ContractPrepayment, invoice.InvoiceKind);
        Assert.Single(await dbContext.InvoiceItems.Where(item => item.InvoiceId == invoice.Id && item.FeeTypeId != null).ToListAsync());
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(string permissionName, CancellationToken ct = default) => Task.FromResult(true);
        public Task<AuthorizationResult> AuthorizeAsync(string permissionName, CancellationToken ct = default) => Task.FromResult(AuthorizationResult.Success());
        public Task EnsurePermissionAsync(string permissionName, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public Task WriteAsync(string action, string entityName, Guid? entityId = null, string? details = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(string? searchText = null, int take = 100, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AuditLogDto>>(Array.Empty<AuditLogDto>());
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public TestCurrentUser(string roleName)
        {
            CurrentUser = new CurrentUserDto
            {
                UserId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                Username = roleName,
                Email = roleName + "@ktx.local",
                FullName = roleName,
                RoleName = roleName
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
}
