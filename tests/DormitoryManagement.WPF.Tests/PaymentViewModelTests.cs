using DormitoryManagement.Application.Abstractions.Auth;
using DormitoryManagement.Application.DTOs.Auth;
using DormitoryManagement.Application.DTOs.Payments;
using DormitoryManagement.Application.Services.Payments;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Enums;
using DormitoryManagement.WPF.ViewModels.Billing;

namespace DormitoryManagement.WPF.Tests;

public sealed class PaymentViewModelTests
{
    [Fact]
    public void Selecting_contract_prepayment_invoice_sets_full_remaining_amount()
    {
        var viewModel = new PaymentViewModel(new StubPaymentService(), StudentUser(), new StubPaymentExtensionService());
        var invoice = new OutstandingInvoiceDto
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-PREPAY",
            InvoiceKind = InvoiceKind.ContractPrepayment,
            RemainingAmount = 4500000m,
            TotalAmount = 4500000m
        };

        viewModel.OutstandingInvoices.Add(invoice);
        viewModel.SelectedInvoice = invoice;

        Assert.Equal(4500000m, viewModel.Amount);
        Assert.True(viewModel.CanCreatePayment);
    }

    [Fact]
    public void Monthly_utility_invoice_enables_extension_request_command()
    {
        var viewModel = new PaymentViewModel(new StubPaymentService(), StudentUser(), new StubPaymentExtensionService());
        var invoice = new OutstandingInvoiceDto
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-UTILITY",
            InvoiceKind = InvoiceKind.MonthlyUtility,
            RemainingAmount = 100000m,
            TotalAmount = 100000m
        };

        viewModel.OutstandingInvoices.Add(invoice);
        viewModel.SelectedInvoice = invoice;
        viewModel.ExtensionRequestedDueDate = new DateTime(2026, 6, 15);
        viewModel.ExtensionReason = "Need more time";

        Assert.True(viewModel.CanRequestExtension);
    }

    private static ICurrentUserService StudentUser() => new StubCurrentUser(RoleNames.Student, Guid.NewGuid());

    private sealed class StubPaymentService : IPaymentService
    {
        public Task<IReadOnlyList<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OutstandingInvoiceDto>>(Array.Empty<OutstandingInvoiceDto>());

        public Task<IReadOnlyList<PaymentDto>> GetPendingPaymentsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PaymentDto>>(Array.Empty<PaymentDto>());

        public Task<PaymentDto> CreateMockPaymentAsync(CreatePaymentRequest request, CancellationToken ct = default) =>
            Task.FromResult(new PaymentDto { Id = Guid.NewGuid(), PaymentCode = "PAY-1", Status = PaymentStatus.Pending });

        public Task<PaymentDto> ConfirmPaymentAsync(ConfirmPaymentRequest request, CancellationToken ct = default) =>
            Task.FromResult(new PaymentDto { Id = request.PaymentId, PaymentCode = "PAY-1", Status = PaymentStatus.Success });

        public Task AllocatePaymentAsync(Guid paymentId, Guid invoiceId, decimal amount, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task CancelPaymentAsync(Guid paymentId, string reason, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class StubPaymentExtensionService : IPaymentExtensionService
    {
        public Task<PaymentExtensionDto> RequestExtensionAsync(CreatePaymentExtensionRequest request, CancellationToken ct = default) =>
            Task.FromResult(new PaymentExtensionDto { Id = Guid.NewGuid(), InvoiceId = request.InvoiceId, Status = PaymentExtensionStatus.Pending });

        public Task<IReadOnlyList<PaymentExtensionDto>> GetPendingExtensionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PaymentExtensionDto>>(Array.Empty<PaymentExtensionDto>());

        public Task ApproveExtensionAsync(Guid extensionId, CancellationToken ct = default) => Task.CompletedTask;
        public Task RejectExtensionAsync(Guid extensionId, string reason, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubCurrentUser : ICurrentUserService
    {
        public StubCurrentUser(string roleName, Guid studentId)
        {
            CurrentUser = new CurrentUserDto
            {
                UserId = Guid.NewGuid(),
                Username = roleName,
                Email = roleName + "@ktx.local",
                FullName = roleName,
                RoleName = roleName,
                StudentId = studentId
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
