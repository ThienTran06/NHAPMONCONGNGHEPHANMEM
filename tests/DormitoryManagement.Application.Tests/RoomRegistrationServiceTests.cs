using DormitoryManagement.Application.Abstractions.Auth;
using DormitoryManagement.Application.Abstractions.Data;
using DormitoryManagement.Application.Abstractions.Services;
using DormitoryManagement.Application.DTOs.Auth;
using DormitoryManagement.Application.DTOs.Audit;
using DormitoryManagement.Application.DTOs.Registrations;
using DormitoryManagement.Application.Security;
using DormitoryManagement.Application.Services.Registrations;
using DormitoryManagement.Domain.Common;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class RoomRegistrationServiceTests
{
    [Fact]
    public async Task CreateRegistrationAsync_creates_pending_registration_when_student_can_register_room()
    {
        var fixture = RegistrationFixture.CreateStudent();

        var registrationId = await fixture.Service.CreateRegistrationAsync(new CreateRoomRegistrationRequest
        {
            RoomId = fixture.Room.Id,
            ContractTermMonths = 6,
            IncludesInternet = true
        });

        var registration = Assert.Single(fixture.UnitOfWork.Set<RoomRegistration>().Items);
        Assert.Equal(registration.Id, registrationId);
        Assert.Equal(RegistrationStatus.Pending, registration.Status);
        Assert.Equal(fixture.Student.Id, registration.StudentId);
        Assert.Equal(fixture.Room.Id, registration.RoomId);
        Assert.Equal(6, registration.ContractTermMonths);
        Assert.True(registration.IncludesInternet);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(13)]
    public async Task CreateRegistrationAsync_rejects_unsupported_contract_terms(int termMonths)
    {
        var fixture = RegistrationFixture.CreateStudent();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateRegistrationAsync(new CreateRoomRegistrationRequest
        {
            RoomId = fixture.Room.Id,
            ContractTermMonths = termMonths
        }));

        Assert.Empty(fixture.UnitOfWork.Set<RoomRegistration>().Items);
    }

    [Fact]
    public async Task CreateRegistrationAsync_rejects_duplicate_pending_registration_for_student()
    {
        var fixture = RegistrationFixture.CreateStudent();
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Pending
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateRegistrationAsync(new CreateRoomRegistrationRequest
        {
            RoomId = fixture.Room.Id
        }));

        Assert.Single(fixture.UnitOfWork.Set<RoomRegistration>().Items);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task CreateRegistrationAsync_rejects_approved_registration_for_student()
    {
        var fixture = RegistrationFixture.CreateStudent();
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Approved
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateRegistrationAsync(new CreateRoomRegistrationRequest
        {
            RoomId = fixture.Room.Id
        }));

        Assert.Single(fixture.UnitOfWork.Set<RoomRegistration>().Items);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task CreateRegistrationAsync_rejects_room_when_active_assignments_plus_pending_holds_fill_capacity()
    {
        var fixture = RegistrationFixture.CreateStudent(roomCapacity: 2);
        var otherStudentId = Guid.NewGuid();
        fixture.UnitOfWork.Set<RoomAssignment>().Items.Add(new RoomAssignment
        {
            Id = Guid.NewGuid(),
            StudentId = otherStudentId,
            RoomId = fixture.Room.Id,
            IsActive = true
        });
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Pending
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateRegistrationAsync(new CreateRoomRegistrationRequest
        {
            RoomId = fixture.Room.Id
        }));

        Assert.Single(fixture.UnitOfWork.Set<RoomRegistration>().Items);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task ApproveRegistrationAsync_creates_pending_contract_and_prepayment_invoice_without_assignment()
    {
        var fixture = RegistrationFixture.CreateManager(roomCapacity: 1);
        var registration = new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Pending,
            ContractTermMonths = 6,
            IncludesInternet = true
        };
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(registration);
        var startDate = new DateTime(2026, 6, 1);

        await fixture.Service.ApproveRegistrationAsync(new ApproveRoomRegistrationRequest
        {
            RegistrationId = registration.Id,
            StartDate = startDate
        });

        Assert.Equal(RegistrationStatus.PaymentPending, registration.Status);
        Assert.Empty(fixture.UnitOfWork.Set<RoomAssignment>().Items);
        Assert.Equal(0, fixture.Room.CurrentOccupancy);
        Assert.Equal(RoomStatus.Available, fixture.Room.Status);
        Assert.Null(fixture.Student.CurrentRoomId);

        var contract = Assert.Single(fixture.UnitOfWork.Set<Contract>().Items);
        Assert.Equal(ContractStatus.PendingPayment, contract.Status);
        Assert.Equal(startDate, contract.StartDate);
        Assert.Equal(startDate.AddMonths(6).AddDays(-1), contract.EndDate);
        Assert.Equal(fixture.Room.MonthlyPrice, contract.MonthlyFee);
        Assert.Equal(6, contract.TermMonths);
        Assert.Equal(4500000m, contract.TotalAmount);
        Assert.True(contract.IncludesInternet);
        Assert.Equal(registration.Id, contract.RoomRegistrationId);
        Assert.Equal(0m, contract.DepositAmount);
        var invoice = Assert.Single(fixture.UnitOfWork.Set<Invoice>().Items);
        Assert.Equal(InvoiceKind.ContractPrepayment, invoice.InvoiceKind);
        Assert.Equal(contract.Id, invoice.ContractId);
        Assert.Equal(contract.TotalAmount, invoice.TotalAmount);
        Assert.Equal(startDate, invoice.DueDate);
        Assert.Equal(invoice.Id, contract.UpfrontInvoiceId);
        Assert.Contains(fixture.AuditLog.Entries, entry => entry.Action == "RoomRegistration.ApprovedPendingPayment");
        Assert.Contains(fixture.AuditLog.Entries, entry => entry.Action == "Contract.PrepaymentInvoiceCreated");
        Assert.True(fixture.UnitOfWork.LastTransaction?.Committed);
    }

    [Fact]
    public async Task ApproveRegistrationAsync_cancels_other_pending_registrations_for_student()
    {
        var fixture = RegistrationFixture.CreateManager(roomCapacity: 2);
        var otherRoom = fixture.CreateRoom("102", capacity: 2);
        var approvedRegistration = new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Pending
        };
        var otherPendingRegistration = new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = otherRoom.Id,
            Status = RegistrationStatus.Pending
        };
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(approvedRegistration);
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(otherPendingRegistration);

        await fixture.Service.ApproveRegistrationAsync(new ApproveRoomRegistrationRequest
        {
            RegistrationId = approvedRegistration.Id,
            StartDate = new DateTime(2026, 6, 1)
        });

        Assert.Equal(RegistrationStatus.PaymentPending, approvedRegistration.Status);
        Assert.Equal(RegistrationStatus.Cancelled, otherPendingRegistration.Status);
        Assert.Empty(fixture.UnitOfWork.Set<RoomAssignment>().Items);
        Assert.Equal(0, fixture.Room.CurrentOccupancy);
        Assert.Equal(0, otherRoom.CurrentOccupancy);
    }

    [Fact]
    public async Task GetPendingRegistrationsAsync_excludes_pending_registrations_for_already_assigned_students()
    {
        var fixture = RegistrationFixture.CreateManager(roomCapacity: 2);
        var registration = new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(registration);
        fixture.UnitOfWork.Set<RoomAssignment>().Items.Add(new RoomAssignment
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            IsActive = true
        });

        var registrations = await fixture.Service.GetPendingRegistrationsAsync();

        Assert.Empty(registrations);
    }

    [Fact]
    public async Task GetPendingRegistrationsAsync_excludes_pending_registrations_for_already_approved_students()
    {
        var fixture = RegistrationFixture.CreateManager(roomCapacity: 2);
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Pending,
            RequestedAt = DateTime.UtcNow
        });
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Approved,
            RequestedAt = DateTime.UtcNow.AddDays(-1)
        });

        var registrations = await fixture.Service.GetPendingRegistrationsAsync();

        Assert.Empty(registrations);
    }

    [Fact]
    public async Task GetPendingRegistrationsAsync_excludes_pending_registrations_for_payment_pending_students()
    {
        var fixture = RegistrationFixture.CreateManager(roomCapacity: 2);
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Pending,
            RequestedAt = DateTime.UtcNow
        });
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.PaymentPending,
            RequestedAt = DateTime.UtcNow.AddDays(-1)
        });

        var registrations = await fixture.Service.GetPendingRegistrationsAsync();

        Assert.Empty(registrations);
    }

    [Fact]
    public async Task ApproveRegistrationAsync_rejects_non_pending_registration()
    {
        var fixture = RegistrationFixture.CreateManager();
        var registration = new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = fixture.Student.Id,
            RoomId = fixture.Room.Id,
            Status = RegistrationStatus.Approved
        };
        fixture.UnitOfWork.Set<RoomRegistration>().Items.Add(registration);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ApproveRegistrationAsync(new ApproveRoomRegistrationRequest
        {
            RegistrationId = registration.Id,
            StartDate = new DateTime(2026, 6, 1)
        }));

        Assert.Empty(fixture.UnitOfWork.Set<RoomAssignment>().Items);
        Assert.Empty(fixture.UnitOfWork.Set<Contract>().Items);
        Assert.False(fixture.UnitOfWork.LastTransaction?.Committed ?? false);
    }

    private sealed class RegistrationFixture
    {
        private RegistrationFixture(string roleName, int roomCapacity)
        {
            Student = new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "SV001",
                FullName = "Nguyen Van An",
                Gender = "Male",
                UserId = Guid.NewGuid()
            };
            Room = new Room
            {
                Id = Guid.NewGuid(),
                BuildingId = Guid.NewGuid(),
                FloorId = Guid.NewGuid(),
                RoomNumber = "101",
                Capacity = roomCapacity,
                CurrentOccupancy = 0,
                MonthlyPrice = 750000m,
                Status = RoomStatus.Available,
                GenderType = RoomGenderType.Male
            };

            UnitOfWork = new FakeUnitOfWork();
            UnitOfWork.Set<Student>().Items.Add(Student);
            UnitOfWork.Set<Room>().Items.Add(Room);
            AuditLog = new FakeAuditLogService();
            CurrentUser = new FakeCurrentUser(new CurrentUserDto
            {
                UserId = roleName == RoleNames.Student ? Student.UserId!.Value : Guid.NewGuid(),
                Username = roleName,
                Email = roleName + "@ktx.local",
                FullName = roleName,
                RoleName = roleName,
                StudentId = roleName == RoleNames.Student ? Student.Id : null,
                BuildingId = roleName == RoleNames.BuildingManager ? Room.BuildingId : null
            });
            Service = new RoomRegistrationService(new FakePermissionService(), UnitOfWork, AuditLog, CurrentUser);
        }

        public RoomRegistrationService Service { get; }
        public FakeUnitOfWork UnitOfWork { get; }
        public FakeAuditLogService AuditLog { get; }
        public FakeCurrentUser CurrentUser { get; }
        public Student Student { get; }
        public Room Room { get; }

        public static RegistrationFixture CreateStudent(int roomCapacity = 4) => new(RoleNames.Student, roomCapacity);
        public static RegistrationFixture CreateManager(int roomCapacity = 4) => new(RoleNames.BuildingManager, roomCapacity);

        public Room CreateRoom(string roomNumber, int capacity = 4, RoomGenderType genderType = RoomGenderType.Male)
        {
            var room = new Room
            {
                Id = Guid.NewGuid(),
                BuildingId = Room.BuildingId,
                FloorId = Room.FloorId,
                RoomNumber = roomNumber,
                Capacity = capacity,
                CurrentOccupancy = 0,
                MonthlyPrice = Room.MonthlyPrice,
                Status = RoomStatus.Available,
                GenderType = genderType
            };
            UnitOfWork.Set<Room>().Items.Add(room);
            return room;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        private readonly Dictionary<Type, object> _repositories = new();

        public int SaveChangesCount { get; private set; }
        public FakeTransaction? LastTransaction { get; private set; }

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
            LastTransaction = new FakeTransaction();
            return Task.FromResult<IUnitOfWorkTransaction>(LastTransaction);
        }
    }

    private sealed class InMemoryRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
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

    private sealed class FakePermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(string permissionName, CancellationToken ct = default) => Task.FromResult(true);
        public Task<AuthorizationResult> AuthorizeAsync(string permissionName, CancellationToken ct = default) => Task.FromResult(AuthorizationResult.Success());
        public Task EnsurePermissionAsync(string permissionName, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAuditLogService : IAuditLogService
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

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public FakeCurrentUser(CurrentUserDto currentUser)
        {
            CurrentUser = currentUser;
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

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }

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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
