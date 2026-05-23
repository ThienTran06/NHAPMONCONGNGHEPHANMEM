using DormitoryManagement.Application.DTOs.Vehicles;
using DormitoryManagement.Application.Services.Vehicles;
using DormitoryManagement.Domain.Enums;
using DormitoryManagement.WPF.ViewModels.Vehicles;

namespace DormitoryManagement.WPF.Tests;

public sealed class VehicleRegistrationViewModelTests
{
    [Fact]
    public void Constructor_exposes_allowed_month_options_and_default_selection()
    {
        var viewModel = new VehicleRegistrationViewModel(new StubVehicleService());

        Assert.Equal(new[] { 1, 2, 3, 6, 9, 12 }, viewModel.MonthOptions);
        Assert.Equal(1, viewModel.SelectedMonthCount);
        Assert.False(viewModel.HasVehicles);
    }

    [Fact]
    public async Task SubmitCommand_sends_license_plate_and_selected_month_to_service()
    {
        var service = new StubVehicleService();
        var viewModel = new VehicleRegistrationViewModel(service)
        {
            LicensePlate = "59a12345",
            SelectedMonthCount = 6
        };

        viewModel.SubmitCommand.Execute(null);

        var request = await service.WaitForRequestAsync();
        Assert.Equal("59a12345", request.LicensePlate);
        Assert.Equal(6, request.MonthCount);
        Assert.True(await WaitUntilAsync(() => viewModel.Vehicles.Count == 1));
        Assert.Equal("Đăng ký giữ xe thành công. Hóa đơn đã được tạo trong Billing.", viewModel.SuccessMessage);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!cts.IsCancellationRequested)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10, cts.Token);
        }

        return false;
    }

    private sealed class StubVehicleService : IVehicleService
    {
        private readonly TaskCompletionSource<CreateVehicleRegistrationRequest> _requestSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CreateVehicleRegistrationRequest> WaitForRequestAsync() => _requestSource.Task;

        public Task<IReadOnlyList<VehicleRegistrationDto>> GetCurrentStudentVehicleRegistrationsAsync(DateTime? asOfDate = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<VehicleRegistrationDto>>(Array.Empty<VehicleRegistrationDto>());

        public Task<VehicleRegistrationDto> RegisterVehicleAsync(CreateVehicleRegistrationRequest request, CancellationToken ct = default)
        {
            _requestSource.TrySetResult(request);
            return Task.FromResult(new VehicleRegistrationDto
            {
                Id = Guid.NewGuid(),
                LicensePlate = "59A1-2345",
                NormalizedPlate = "59A1-2345",
                MonthCount = request.MonthCount,
                Amount = request.MonthCount * 40000m,
                Status = VehicleStatus.Pending,
                StatusText = "Chưa thanh toán"
            });
        }

        public Task ApproveVehicleAsync(Guid registrationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task RejectVehicleAsync(Guid registrationId, string reason, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelVehicleAsync(Guid registrationId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
