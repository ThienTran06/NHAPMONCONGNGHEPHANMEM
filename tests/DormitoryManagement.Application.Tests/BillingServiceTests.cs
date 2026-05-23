using DormitoryManagement.Application.DTOs.Billing;
using DormitoryManagement.Application.Services.Billing;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class BillingServiceTests
{
    [Fact]
    public async Task GenerateMonthlyInvoicesAsync_creates_utility_only_invoices_split_by_active_room_occupants()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV002", FullName = "Tran Thi Binh", UserId = Guid.NewGuid() };
        var roommate = new Student { Id = Guid.NewGuid(), StudentCode = "SV003", FullName = "Le Minh Chau", UserId = Guid.NewGuid() };
        var room = new Room { Id = Guid.NewGuid(), RoomNumber = "A-102", MonthlyPrice = 750000m };
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            RoomId = room.Id,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            MonthlyFee = 750000m,
            IncludesInternet = true,
            Status = ContractStatus.Active
        };
        var roommateContract = new Contract
        {
            Id = Guid.NewGuid(),
            StudentId = roommate.Id,
            RoomId = room.Id,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            MonthlyFee = 750000m,
            IncludesInternet = false,
            Status = ContractStatus.Active
        };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Student>().Items.Add(roommate);
        unitOfWork.Set<Room>().Items.Add(room);
        unitOfWork.Set<Contract>().Items.Add(contract);
        unitOfWork.Set<Contract>().Items.Add(roommateContract);
        unitOfWork.Set<FeeType>().Items.AddRange(new[]
        {
            FeeType("ROOM_FEE"),
            FeeType("INTERNET"),
            FeeType("ELECTRICITY"),
            FeeType("WATER"),
            FeeType("PARKING")
        });
        unitOfWork.Set<FeeRate>().Items.AddRange(unitOfWork.Set<FeeType>().Items.Select(type => new FeeRate
        {
            Id = Guid.NewGuid(),
            FeeTypeId = type.Id,
            Amount = type.Code switch
            {
                "ROOM_FEE" => 750000m,
                "INTERNET" => 50000m,
                "ELECTRICITY" => 3500m,
                "WATER" => 19500m,
                "PARKING" => 100000m,
                _ => 0m
            },
            EffectiveFrom = new DateTime(2026, 1, 1)
        }));
        unitOfWork.Set<UtilityReading>().Items.Add(new UtilityReading
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            BillingPeriod = "2026-06",
            ElectricityPrevious = 100m,
            ElectricityCurrent = 120m,
            WaterPrevious = 10m,
            WaterCurrent = 13m
        });
        var audit = new RecordingAuditLogService();
        var service = new BillingService(
            new AllowAllPermissionService(),
            unitOfWork,
            audit,
            new TestCurrentUser(RoleNames.Manager));

        var result = await service.GenerateMonthlyInvoicesAsync(new GenerateMonthlyInvoiceRequest
        {
            BillingPeriod = "2026-06",
            DueDate = new DateTime(2026, 6, 15)
        });

        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.MissingUtilityReadingCount);
        var invoice = Assert.Single(unitOfWork.Set<Invoice>().Items, candidate => candidate.StudentId == student.Id);
        var roommateInvoice = Assert.Single(unitOfWork.Set<Invoice>().Items, candidate => candidate.StudentId == roommate.Id);
        Assert.Equal(student.Id, invoice.StudentId);
        Assert.Equal(room.Id, invoice.RoomId);
        Assert.Equal("2026-06", invoice.BillingPeriod);
        Assert.Equal(InvoiceKind.MonthlyUtility, invoice.InvoiceKind);
        Assert.Equal(new DateTime(2026, 6, 10), invoice.DueDate);
        Assert.Equal(114250m, invoice.TotalAmount);
        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
        Assert.Equal(3, invoice.Items.Count);
        Assert.Contains(invoice.Items, item => item.Description == "Electricity 2026-06" && item.Quantity == 10m && item.Amount == 35000m);
        Assert.Contains(invoice.Items, item => item.Description == "Water 2026-06" && item.Quantity == 1.5m && item.Amount == 29250m);
        Assert.Contains(invoice.Items, item => item.Description == "Internet 2026-06" && item.Quantity == 1m && item.Amount == 50000m);
        Assert.Equal(64250m, roommateInvoice.TotalAmount);
        Assert.DoesNotContain(invoice.Items, item => item.Description.StartsWith("Room fee", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(invoice.Items, item => item.Description.StartsWith("Parking", StringComparison.OrdinalIgnoreCase));
        Assert.True(unitOfWork.LastTransaction?.Committed);
        Assert.Contains(audit.Entries, entry => entry.Action == "Invoice.GeneratedMonthly");
    }

    [Fact]
    public async Task GenerateMonthlyInvoicesAsync_skips_room_when_utility_reading_is_missing()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV002", FullName = "Tran Thi Binh" };
        var room = new Room { Id = Guid.NewGuid(), RoomNumber = "A-102", MonthlyPrice = 750000m };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Room>().Items.Add(room);
        unitOfWork.Set<Contract>().Items.Add(new Contract
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            RoomId = room.Id,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = ContractStatus.Active
        });
        unitOfWork.Set<FeeType>().Items.AddRange(new[] { FeeType("ELECTRICITY"), FeeType("WATER"), FeeType("INTERNET") });
        unitOfWork.Set<FeeRate>().Items.AddRange(unitOfWork.Set<FeeType>().Items.Select(type => new FeeRate
        {
            Id = Guid.NewGuid(),
            FeeTypeId = type.Id,
            Amount = type.Code == "WATER" ? 19500m : type.Code == "ELECTRICITY" ? 3500m : 50000m,
            EffectiveFrom = new DateTime(2026, 1, 1)
        }));
        var service = new BillingService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Manager));

        var result = await service.GenerateMonthlyInvoicesAsync(new GenerateMonthlyInvoiceRequest
        {
            BillingPeriod = "2026-06",
            DueDate = new DateTime(2026, 6, 15)
        });

        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.MissingUtilityReadingCount);
        Assert.Empty(unitOfWork.Set<Invoice>().Items);
        Assert.Contains(result.Warnings, warning => warning.Contains(room.RoomNumber, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpsertUtilityReadingAsync_uses_previous_period_current_values()
    {
        var room = new Room { Id = Guid.NewGuid(), RoomNumber = "A-102", BuildingId = Guid.NewGuid() };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Room>().Items.Add(room);
        unitOfWork.Set<UtilityReading>().Items.Add(new UtilityReading
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            BillingPeriod = "2026-05",
            ElectricityCurrent = 120m,
            WaterCurrent = 13m
        });
        var service = new BillingService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.BuildingManager, buildingId: room.BuildingId));

        await service.UpsertUtilityReadingAsync(new UtilityReadingRequest
        {
            RoomId = room.Id,
            BillingPeriod = "2026-06",
            ElectricityCurrent = 140m,
            WaterCurrent = 16m
        });

        var reading = Assert.Single(unitOfWork.Set<UtilityReading>().Items, item => item.BillingPeriod == "2026-06");
        Assert.Equal(120m, reading.ElectricityPrevious);
        Assert.Equal(13m, reading.WaterPrevious);
    }

    [Fact]
    public async Task GetInvoicesAsync_marks_unpaid_past_due_vehicle_invoice_overdue()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An" };
        var room = new Room { Id = Guid.NewGuid(), RoomNumber = "A-101" };
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-PARK-202605-ABC",
            StudentId = student.Id,
            RoomId = room.Id,
            BillingPeriod = DateTime.UtcNow.ToString("yyyy-MM"),
            InvoiceKind = InvoiceKind.VehicleParking,
            IssueDate = DateTime.UtcNow.Date.AddDays(-3),
            DueDate = DateTime.UtcNow.Date.AddDays(-1),
            TotalAmount = 40000m,
            PaidAmount = 0m,
            Status = InvoiceStatus.Unpaid
        };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Room>().Items.Add(room);
        unitOfWork.Set<Invoice>().Items.Add(invoice);
        var service = new BillingService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Manager));

        var invoices = await service.GetInvoicesAsync();

        var dto = Assert.Single(invoices);
        Assert.Equal(InvoiceStatus.Overdue, dto.Status);
        Assert.Equal(InvoiceStatus.Overdue, invoice.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    private static FeeType FeeType(string code) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            IsRecurring = true
        };
}
