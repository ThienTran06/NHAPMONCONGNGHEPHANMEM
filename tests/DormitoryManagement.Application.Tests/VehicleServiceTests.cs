using DormitoryManagement.Application.DTOs.Vehicles;
using DormitoryManagement.Application.Services.Vehicles;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class VehicleServiceTests
{
    [Theory]
    [InlineData("59A1-2345", "59A1-2345")]
    [InlineData("59a1-23456", "59A1-23456")]
    [InlineData("59A12345", "59A1-2345")]
    [InlineData("59a123456", "59A1-23456")]
    public async Task RegisterVehicleAsync_normalizes_license_plate_creates_vehicle_and_invoice(string input, string expectedPlate)
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An", CurrentRoomId = Guid.NewGuid() };
        var room = new Room { Id = student.CurrentRoomId.Value, RoomNumber = "A-101" };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Room>().Items.Add(room);
        var audit = new RecordingAuditLogService();
        var service = new VehicleService(
            new AllowAllPermissionService(),
            unitOfWork,
            audit,
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        var dto = await service.RegisterVehicleAsync(new CreateVehicleRegistrationRequest
        {
            LicensePlate = input,
            MonthCount = 3
        });

        var registration = Assert.Single(unitOfWork.Set<VehicleRegistration>().Items);
        Assert.Equal(student.Id, registration.StudentId);
        Assert.Equal(expectedPlate, registration.LicensePlate);
        Assert.Equal(3, registration.MonthCount);
        Assert.Equal(120000m, registration.Amount);
        Assert.Equal(InvoiceStatus.Unpaid, dto.InvoiceStatus);
        Assert.Equal("Chưa thanh toán", dto.StatusText);

        var invoice = Assert.Single(unitOfWork.Set<Invoice>().Items);
        Assert.Equal(student.Id, invoice.StudentId);
        Assert.Equal(room.Id, invoice.RoomId);
        Assert.Equal(InvoiceKind.VehicleParking, invoice.InvoiceKind);
        Assert.StartsWith("INV-PARK-", invoice.InvoiceNumber);
        Assert.Equal(120000m, invoice.TotalAmount);
        Assert.Equal(0m, invoice.PaidAmount);
        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
        Assert.Equal(invoice.IssueDate.Date.AddDays(2), invoice.DueDate.Date);
        Assert.Equal(invoice.Id, registration.InvoiceId);

        var item = Assert.Single(unitOfWork.Set<InvoiceItem>().Items);
        Assert.Equal(invoice.Id, item.InvoiceId);
        Assert.Contains(expectedPlate, item.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3m, item.Quantity);
        Assert.Equal(40000m, item.UnitPrice);
        Assert.Equal(120000m, item.Amount);
        Assert.True(unitOfWork.LastTransaction?.Committed);
        Assert.Contains(audit.Entries, entry => entry.Action == "Invoice.Created");
    }

    [Theory]
    [InlineData("59AA-2345")]
    [InlineData("59A-2345")]
    [InlineData("59A1234")]
    [InlineData("59A1234567")]
    public async Task RegisterVehicleAsync_rejects_invalid_license_plate(string input)
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An", CurrentRoomId = Guid.NewGuid() };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Room>().Items.Add(new Room { Id = student.CurrentRoomId.Value, RoomNumber = "A-101" });
        var service = new VehicleService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterVehicleAsync(new CreateVehicleRegistrationRequest
        {
            LicensePlate = input,
            MonthCount = 1
        }));

        Assert.Equal("Nhập sai định dạng biển số.", exception.Message);
        Assert.Empty(unitOfWork.Set<VehicleRegistration>().Items);
        Assert.Empty(unitOfWork.Set<Invoice>().Items);
    }

    [Fact]
    public async Task RegisterVehicleAsync_rejects_student_without_current_room()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An" };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        var service = new VehicleService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterVehicleAsync(new CreateVehicleRegistrationRequest
        {
            LicensePlate = "59A12345",
            MonthCount = 1
        }));
    }

    [Fact]
    public async Task RegisterVehicleAsync_rejects_duplicate_active_normalized_plate()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An", CurrentRoomId = Guid.NewGuid() };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Room>().Items.Add(new Room { Id = student.CurrentRoomId.Value, RoomNumber = "A-101" });
        unitOfWork.Set<VehicleRegistration>().Items.Add(new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            LicensePlate = "59a12345",
            Status = VehicleStatus.Approved
        });
        var service = new VehicleService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterVehicleAsync(new CreateVehicleRegistrationRequest
        {
            LicensePlate = "59a12345",
            MonthCount = 1
        }));

        Assert.Single(unitOfWork.Set<VehicleRegistration>().Items);
        Assert.Empty(unitOfWork.Set<Invoice>().Items);
    }

    [Fact]
    public async Task RegisterVehicleAsync_allows_duplicate_plate_after_previous_registration_expired()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An", CurrentRoomId = Guid.NewGuid() };
        var oldInvoiceId = Guid.NewGuid();
        var oldPaymentDate = DateTime.UtcNow.Date.AddMonths(-2);
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Room>().Items.Add(new Room { Id = student.CurrentRoomId.Value, RoomNumber = "A-101" });
        unitOfWork.Set<VehicleRegistration>().Items.Add(new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            LicensePlate = "59A1-2345",
            Status = VehicleStatus.Approved,
            MonthCount = 1,
            PaymentDate = oldPaymentDate,
            InvoiceId = oldInvoiceId
        });
        unitOfWork.Set<Invoice>().Items.Add(new Invoice
        {
            Id = oldInvoiceId,
            StudentId = Guid.NewGuid(),
            RoomId = student.CurrentRoomId.Value,
            InvoiceNumber = "INV-OLD",
            BillingPeriod = oldPaymentDate.ToString("yyyy-MM"),
            TotalAmount = 40000m,
            PaidAmount = 40000m,
            Status = InvoiceStatus.Paid
        });
        var service = new VehicleService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        var dto = await service.RegisterVehicleAsync(new CreateVehicleRegistrationRequest
        {
            LicensePlate = "59a12345",
            MonthCount = 1
        });

        Assert.Equal("59A1-2345", dto.NormalizedPlate);
        Assert.Equal(2, unitOfWork.Set<VehicleRegistration>().Items.Count);
        Assert.Equal(2, unitOfWork.Set<Invoice>().Items.Count);
    }

    [Fact]
    public async Task GetCurrentStudentVehicleRegistrationsAsync_maps_payment_status_and_expiry()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An", CurrentRoomId = Guid.NewGuid() };
        var paidInvoiceId = Guid.NewGuid();
        var unpaidInvoiceId = Guid.NewGuid();
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<VehicleRegistration>().Items.AddRange(new[]
        {
            new VehicleRegistration
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                LicensePlate = "59A1-2345",
                MonthCount = 1,
                Amount = 40000m,
                RegisteredAt = new DateTime(2026, 5, 18),
                InvoiceId = paidInvoiceId,
                PaymentDate = new DateTime(2026, 5, 18)
            },
            new VehicleRegistration
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                LicensePlate = "59A1-9999",
                MonthCount = 2,
                Amount = 80000m,
                RegisteredAt = new DateTime(2026, 5, 19),
                InvoiceId = unpaidInvoiceId
            }
        });
        unitOfWork.Set<Invoice>().Items.AddRange(new[]
        {
            new Invoice { Id = paidInvoiceId, StudentId = student.Id, RoomId = student.CurrentRoomId.Value, InvoiceNumber = "INV-PAID", BillingPeriod = "2026-05", TotalAmount = 40000m, PaidAmount = 40000m, Status = InvoiceStatus.Paid },
            new Invoice { Id = unpaidInvoiceId, StudentId = student.Id, RoomId = student.CurrentRoomId.Value, InvoiceNumber = "INV-UNPAID", BillingPeriod = "2026-05", TotalAmount = 80000m, PaidAmount = 0m, Status = InvoiceStatus.Unpaid }
        });
        var service = new VehicleService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        var registrations = await service.GetCurrentStudentVehicleRegistrationsAsync(new DateTime(2026, 5, 19));

        Assert.Collection(registrations,
            paid =>
            {
                Assert.Equal(1, paid.RowNumber);
                Assert.Equal("Đã thanh toán và áp dụng", paid.StatusText);
                Assert.Equal(new DateTime(2026, 5, 18), paid.PaymentDate);
                Assert.Equal(new DateTime(2026, 6, 18), paid.ExpiryDate);
            },
            unpaid =>
            {
                Assert.Equal(2, unpaid.RowNumber);
                Assert.Equal("Chưa thanh toán", unpaid.StatusText);
                Assert.Null(unpaid.PaymentDate);
                Assert.Null(unpaid.ExpiryDate);
            });
    }

    [Fact]
    public async Task GetCurrentStudentVehicleRegistrationsAsync_marks_paid_registration_expired_after_expiry_date()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An", CurrentRoomId = Guid.NewGuid() };
        var invoiceId = Guid.NewGuid();
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<VehicleRegistration>().Items.Add(new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            LicensePlate = "59A1-8888",
            MonthCount = 1,
            Amount = 40000m,
            RegisteredAt = new DateTime(2026, 3, 1),
            InvoiceId = invoiceId,
            PaymentDate = new DateTime(2026, 3, 1)
        });
        unitOfWork.Set<Invoice>().Items.Add(new Invoice
        {
            Id = invoiceId,
            StudentId = student.Id,
            RoomId = student.CurrentRoomId.Value,
            InvoiceNumber = "INV-EXPIRED",
            BillingPeriod = "2026-03",
            TotalAmount = 40000m,
            PaidAmount = 40000m,
            Status = InvoiceStatus.Paid
        });
        var service = new VehicleService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        var registration = Assert.Single(await service.GetCurrentStudentVehicleRegistrationsAsync(new DateTime(2026, 4, 2)));

        Assert.Equal("Hết hạn", registration.StatusText);
        Assert.Equal(new DateTime(2026, 4, 1), registration.ExpiryDate);
    }
}
