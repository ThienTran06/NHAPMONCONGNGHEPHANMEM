using DormitoryManagement.Application.DTOs.Payments;
using DormitoryManagement.Application.Services.Payments;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class PaymentExtensionServiceTests
{
    [Fact]
    public async Task RequestExtensionAsync_allows_student_to_request_own_monthly_utility_invoice()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An" };
        var invoice = MonthlyInvoice(student.Id, new DateTime(2026, 6, 10));
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Invoice>().Items.Add(invoice);
        var audit = new RecordingAuditLogService();
        var service = new PaymentExtensionService(
            new AllowAllPermissionService(),
            unitOfWork,
            audit,
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        var dto = await service.RequestExtensionAsync(new CreatePaymentExtensionRequest
        {
            InvoiceId = invoice.Id,
            RequestedDueDate = new DateTime(2026, 6, 20),
            Reason = "Need a few more days"
        });

        var extension = Assert.Single(unitOfWork.Set<PaymentExtension>().Items);
        Assert.Equal(extension.Id, dto.Id);
        Assert.Equal(PaymentExtensionStatus.Pending, extension.Status);
        Assert.Equal(invoice.Id, extension.InvoiceId);
        Assert.Equal(student.Id, extension.StudentId);
        Assert.Contains(audit.Entries, entry => entry.Action == "PaymentExtension.Requested");
    }

    [Fact]
    public async Task ApproveExtensionAsync_caps_due_date_at_day_15()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An" };
        var room = new Room { Id = Guid.NewGuid(), BuildingId = Guid.NewGuid(), RoomNumber = "A-101" };
        var invoice = MonthlyInvoice(student.Id, new DateTime(2026, 6, 10));
        invoice.RoomId = room.Id;
        var extension = new PaymentExtension
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            StudentId = student.Id,
            RequestedDueDate = new DateTime(2026, 6, 20),
            Reason = "Need a few more days",
            Status = PaymentExtensionStatus.Pending
        };
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Room>().Items.Add(room);
        unitOfWork.Set<Invoice>().Items.Add(invoice);
        unitOfWork.Set<PaymentExtension>().Items.Add(extension);
        var audit = new RecordingAuditLogService();
        var service = new PaymentExtensionService(
            new AllowAllPermissionService(),
            unitOfWork,
            audit,
            new TestCurrentUser(RoleNames.BuildingManager, buildingId: room.BuildingId));

        await service.ApproveExtensionAsync(extension.Id);

        Assert.Equal(PaymentExtensionStatus.Approved, extension.Status);
        Assert.Equal(new DateTime(2026, 6, 15), invoice.DueDate);
        Assert.Contains(audit.Entries, entry => entry.Action == "PaymentExtension.Approved");
    }

    [Fact]
    public async Task RequestExtensionAsync_rejects_contract_prepayment_invoice()
    {
        var student = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Nguyen Van An" };
        var invoice = MonthlyInvoice(student.Id, new DateTime(2026, 6, 1));
        invoice.InvoiceKind = InvoiceKind.ContractPrepayment;
        var unitOfWork = new InMemoryUnitOfWork();
        unitOfWork.Set<Student>().Items.Add(student);
        unitOfWork.Set<Invoice>().Items.Add(invoice);
        var service = new PaymentExtensionService(
            new AllowAllPermissionService(),
            unitOfWork,
            new RecordingAuditLogService(),
            new TestCurrentUser(RoleNames.Student, studentId: student.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestExtensionAsync(new CreatePaymentExtensionRequest
        {
            InvoiceId = invoice.Id,
            RequestedDueDate = new DateTime(2026, 6, 15),
            Reason = "Need a few more days"
        }));
    }

    private static Invoice MonthlyInvoice(Guid studentId, DateTime dueDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-2026-06-001",
            StudentId = studentId,
            RoomId = Guid.NewGuid(),
            BillingPeriod = "2026-06",
            IssueDate = new DateTime(2026, 6, 5),
            DueDate = dueDate,
            TotalAmount = 100000m,
            PaidAmount = 0m,
            Status = InvoiceStatus.Unpaid,
            InvoiceKind = InvoiceKind.MonthlyUtility
        };
}
