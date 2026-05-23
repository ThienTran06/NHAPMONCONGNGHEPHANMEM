using DormitoryManagement.Application.DTOs.Payments;
using DormitoryManagement.Application.Services.Payments;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task CreateMockPaymentAsync_as_student_creates_pending_payment_for_current_student()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An" };
        var invoice = Invoice(student.Id, "INV-PREPAY", new DateTime(2026, 6, 1), 4500000m, 0m, InvoiceStatus.Unpaid);
        invoice.InvoiceKind = InvoiceKind.ContractPrepayment;
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Invoice>().Items.Add(invoice);
        var audit = new RecordingAuditLogService();
        var service = new PaymentService(
            new AllowAllPermissionService(),
            unitOfWork,
            audit,
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        var payment = await service.CreateMockPaymentAsync(new CreatePaymentRequest
        {
            InvoiceId = invoice.Id,
            Amount = 4500000m,
            Method = PaymentMethod.MockGateway
        });

        var entity = Assert.Single(unitOfWork.Set<Payment>().Items);
        Assert.Equal(student.Id, entity.StudentId);
        Assert.Equal(invoice.Id, entity.TargetInvoiceId);
        Assert.Equal(4500000m, entity.Amount);
        Assert.Equal(PaymentStatus.Pending, entity.Status);
        Assert.Equal(entity.Id, payment.Id);
        Assert.Contains(audit.Entries, entry => entry.Action == "Payment.Created");
    }

    [Fact]
    public async Task CreateMockPaymentAsync_rejects_partial_contract_prepayment()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An" };
        var invoice = Invoice(student.Id, "INV-PREPAY", new DateTime(2026, 6, 1), 4500000m, 0m, InvoiceStatus.Unpaid);
        invoice.InvoiceKind = InvoiceKind.ContractPrepayment;
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Invoice>().Items.Add(invoice);
        var service = new PaymentService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateMockPaymentAsync(new CreatePaymentRequest
        {
            InvoiceId = invoice.Id,
            Amount = 1000000m,
            Method = PaymentMethod.MockGateway
        }));

        Assert.Empty(unitOfWork.Set<Payment>().Items);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_targeted_contract_prepayment_activates_assignment_contract_and_registration()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An" };
        var room = new Room { Id = Guid.NewGuid(), RoomNumber = "A-101", Capacity = 2, MonthlyPrice = 750000m, Status = RoomStatus.Available };
        var registration = new RoomRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            RoomId = room.Id,
            Status = RegistrationStatus.PaymentPending
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            RoomId = room.Id,
            RoomRegistrationId = registration.Id,
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = ContractStatus.PendingPayment,
            TotalAmount = 4500000m
        };
        var invoice = Invoice(student.Id, "INV-PREPAY", new DateTime(2026, 6, 1), 4500000m, 0m, InvoiceStatus.Unpaid);
        invoice.InvoiceKind = InvoiceKind.ContractPrepayment;
        invoice.ContractId = contract.Id;
        contract.UpfrontInvoiceId = invoice.Id;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            TargetInvoiceId = invoice.Id,
            PaymentCode = "PAY-PREPAY",
            Amount = 4500000m,
            Status = PaymentStatus.Pending,
            CreatedAt = new DateTime(2026, 5, 1)
        };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Room>().Items.Add(room);
        unitOfWork.Set<RoomRegistration>().Items.Add(registration);
        unitOfWork.Set<Contract>().Items.Add(contract);
        unitOfWork.Set<Invoice>().Items.Add(invoice);
        unitOfWork.Set<Payment>().Items.Add(payment);
        var audit = new RecordingAuditLogService();
        var service = new PaymentService(
            new AllowAllPermissionService(),
            unitOfWork,
            audit,
            new TestCurrentUser(RoleNames.Manager));

        await service.ConfirmPaymentAsync(new ConfirmPaymentRequest { PaymentId = payment.Id, TransactionRef = "BANK-999" });

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(4500000m, invoice.PaidAmount);
        Assert.Equal(ContractStatus.Active, contract.Status);
        Assert.Equal(RegistrationStatus.Approved, registration.Status);
        var assignment = Assert.Single(unitOfWork.Set<RoomAssignment>().Items);
        Assert.True(assignment.IsActive);
        Assert.Equal(student.Id, assignment.StudentId);
        Assert.Equal(room.Id, assignment.RoomId);
        Assert.Equal(1, room.CurrentOccupancy);
        Assert.Equal(room.Id, student.CurrentRoomId);
        Assert.Contains(audit.Entries, entry => entry.Action == "Contract.Activated");
        Assert.Contains(audit.Entries, entry => entry.Action == "RoomAssignment.Created");
    }

    [Fact]
    public async Task ConfirmPaymentAsync_allocates_to_oldest_outstanding_invoices()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV002", FullName = "Tran Thi Binh" };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            PaymentCode = "PAY-DEMO",
            Amount = 120000m,
            Status = PaymentStatus.Pending,
            CreatedAt = new DateTime(2026, 5, 1)
        };
        var olderInvoice = Invoice(student.Id, "INV-OLD", new DateTime(2026, 5, 10), 100000m, 0m, InvoiceStatus.Unpaid);
        var newerInvoice = Invoice(student.Id, "INV-NEW", new DateTime(2026, 5, 20), 50000m, 0m, InvoiceStatus.Unpaid);
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Payment>().Items.Add(payment);
        unitOfWork.Set<Invoice>().Items.AddRange(new[] { newerInvoice, olderInvoice });
        var audit = new RecordingAuditLogService();
        var service = new PaymentService(
            new AllowAllPermissionService(),
            unitOfWork,
            audit,
            new TestCurrentUser(RoleNames.Manager));

        var confirmed = await service.ConfirmPaymentAsync(new ConfirmPaymentRequest
        {
            PaymentId = payment.Id,
            TransactionRef = "BANK-123"
        });

        Assert.Equal(PaymentStatus.Success, payment.Status);
        Assert.Equal(PaymentStatus.Success, confirmed.Status);
        Assert.Equal("BANK-123", payment.TransactionRef);
        Assert.NotNull(payment.PaidAt);
        Assert.Equal(InvoiceStatus.Paid, olderInvoice.Status);
        Assert.Equal(100000m, olderInvoice.PaidAmount);
        Assert.Equal(InvoiceStatus.Partial, newerInvoice.Status);
        Assert.Equal(20000m, newerInvoice.PaidAmount);
        Assert.Collection(
            unitOfWork.Set<PaymentAllocation>().Items.OrderBy(allocation => allocation.Amount),
            allocation =>
            {
                Assert.Equal(newerInvoice.Id, allocation.InvoiceId);
                Assert.Equal(20000m, allocation.Amount);
            },
            allocation =>
            {
                Assert.Equal(olderInvoice.Id, allocation.InvoiceId);
                Assert.Equal(100000m, allocation.Amount);
            });
        Assert.True(unitOfWork.LastTransaction?.Committed);
        Assert.Contains(audit.Entries, entry => entry.Action == "Payment.Confirmed");
    }

    [Fact]
    public async Task ConfirmPaymentAsync_marks_vehicle_registration_payment_date_when_vehicle_invoice_paid()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An" };
        var invoice = Invoice(student.Id, "INV-PARK", DateTime.UtcNow.Date.AddDays(2), 40000m, 0m, InvoiceStatus.Unpaid);
        invoice.InvoiceKind = InvoiceKind.VehicleParking;
        var registration = new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            LicensePlate = "59A1-2345",
            MonthCount = 1,
            Amount = 40000m,
            InvoiceId = invoice.Id
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            TargetInvoiceId = invoice.Id,
            PaymentCode = "PAY-PARK",
            Amount = 40000m,
            Status = PaymentStatus.Pending
        };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Invoice>().Items.Add(invoice);
        unitOfWork.Set<VehicleRegistration>().Items.Add(registration);
        unitOfWork.Set<Payment>().Items.Add(payment);
        var service = new PaymentService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Manager));

        await service.ConfirmPaymentAsync(new ConfirmPaymentRequest { PaymentId = payment.Id, TransactionRef = "BANK-PARK" });

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(payment.PaidAt);
        Assert.Equal(payment.PaidAt.Value.Date, registration.PaymentDate);
    }

    private static Invoice Invoice(Guid studentId, string number, DateTime dueDate, decimal total, decimal paid, InvoiceStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = number,
            StudentId = studentId,
            RoomId = Guid.NewGuid(),
            BillingPeriod = "2026-05",
            IssueDate = new DateTime(2026, 5, 1),
            DueDate = dueDate,
            TotalAmount = total,
            PaidAmount = paid,
            Status = status
        };
}
