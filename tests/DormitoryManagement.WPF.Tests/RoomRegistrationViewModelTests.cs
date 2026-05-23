using DormitoryManagement.Application.Common;
using DormitoryManagement.Application.DTOs.Registrations;
using DormitoryManagement.Application.DTOs.Rooms;
using DormitoryManagement.Application.Services.Registrations;
using DormitoryManagement.Application.Services.Rooms;
using DormitoryManagement.Domain.Enums;
using DormitoryManagement.WPF.ViewModels.Registrations;

namespace DormitoryManagement.WPF.Tests;

public sealed class RoomRegistrationViewModelTests
{
    [Fact]
    public void SelectedFloor_accepts_null_from_combo_box_clear_without_throwing()
    {
        var viewModel = new RoomRegistrationViewModel(new StubRegistrationService(), new StubRoomService());

        viewModel.SelectedFloor = null!;

        Assert.Equal("All floors", viewModel.SelectedFloor);
        Assert.Empty(viewModel.AvailableRooms);
    }

    [Fact]
    public void Filter_selections_accept_null_from_combo_box_clear_without_throwing()
    {
        var viewModel = new RoomRegistrationViewModel(new StubRegistrationService(), new StubRoomService());

        viewModel.SelectedBuilding = null!;
        viewModel.SelectedGender = null!;
        viewModel.SelectedPriceSort = null!;

        Assert.Equal("All buildings", viewModel.SelectedBuilding);
        Assert.Equal("All genders", viewModel.SelectedGender);
        Assert.Equal("Room number", viewModel.SelectedPriceSort);
    }

    [Fact]
    public void Defaults_contract_term_to_12_months_without_internet()
    {
        var viewModel = new RoomRegistrationViewModel(new StubRegistrationService(), new StubRoomService());

        Assert.Equal(12, viewModel.ContractTermMonths);
        Assert.False(viewModel.IncludesInternet);
    }

    private sealed class StubRegistrationService : IRoomRegistrationService
    {
        public CreateRoomRegistrationRequest? LastRequest { get; private set; }

        public Task<Guid> CreateRegistrationAsync(CreateRoomRegistrationRequest request, CancellationToken ct = default) =>
            Task.FromResult(Capture(request));

        private Guid Capture(CreateRoomRegistrationRequest request)
        {
            LastRequest = request;
            return Guid.NewGuid();
        }

        public Task<IReadOnlyList<RoomRegistrationDto>> GetPendingRegistrationsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RoomRegistrationDto>>(Array.Empty<RoomRegistrationDto>());

        public Task<IReadOnlyList<RoomRegistrationDto>> GetCurrentStudentRegistrationsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RoomRegistrationDto>>(Array.Empty<RoomRegistrationDto>());

        public Task ApproveRegistrationAsync(ApproveRoomRegistrationRequest request, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RejectRegistrationAsync(RejectRoomRegistrationRequest request, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task CancelRegistrationAsync(Guid registrationId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class StubRoomService : IRoomService
    {
        public Task<PagedResult<RoomDto>> GetRoomsAsync(RoomFilterRequest? request = null, CancellationToken ct = default) =>
            Task.FromResult(PagedResult<RoomDto>.Empty());

        public Task<IReadOnlyList<RoomDto>> GetAvailableRoomsAsync(RoomFilterRequest? request = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RoomDto>>(Array.Empty<RoomDto>());

        public Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default) =>
            Task.FromResult(new RoomDto());

        public Task<RoomDto> UpdateRoomAsync(Guid roomId, CreateRoomRequest request, CancellationToken ct = default) =>
            Task.FromResult(new RoomDto { Id = roomId });

        public Task ChangeRoomStatusAsync(Guid roomId, RoomStatus status, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
