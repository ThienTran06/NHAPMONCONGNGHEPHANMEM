using DormitoryManagement.Application.DTOs.Rooms;
using DormitoryManagement.Application.Services.Rooms;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class RoomServiceTests
{
    [Fact]
    public async Task GetAvailableRoomsAsync_filters_by_building_floor_gender_and_sorts_price_low_to_high()
    {
        var unitOfWork = CreateRoomFixture(out var buildingAId, out _, out var floorA2Id);
        var service = new RoomService(new InMemoryRoomRepository(unitOfWork), unitOfWork, new AllowAllPermissionService());

        var rooms = await service.GetAvailableRoomsAsync(new RoomFilterRequest
        {
            BuildingId = buildingAId,
            FloorId = floorA2Id,
            GenderType = RoomGenderType.Female,
            PriceSortOrder = RoomPriceSortOrder.LowToHigh
        });

        Assert.Collection(rooms,
            room => Assert.Equal(600000m, room.MonthlyPrice),
            room => Assert.Equal(750000m, room.MonthlyPrice));
        Assert.All(rooms, room =>
        {
            Assert.Equal(buildingAId, room.BuildingId);
            Assert.Equal(floorA2Id, room.FloorId);
            Assert.Equal(RoomGenderType.Female, room.GenderType);
        });
    }

    [Fact]
    public async Task GetAvailableRoomsAsync_sorts_price_high_to_low()
    {
        var unitOfWork = CreateRoomFixture(out var buildingAId, out _, out var floorA2Id);
        var service = new RoomService(new InMemoryRoomRepository(unitOfWork), unitOfWork, new AllowAllPermissionService());

        var rooms = await service.GetAvailableRoomsAsync(new RoomFilterRequest
        {
            BuildingId = buildingAId,
            FloorId = floorA2Id,
            GenderType = RoomGenderType.Female,
            PriceSortOrder = RoomPriceSortOrder.HighToLow
        });

        Assert.Collection(rooms,
            room => Assert.Equal(750000m, room.MonthlyPrice),
            room => Assert.Equal(600000m, room.MonthlyPrice));
    }

    private static InMemoryUnitOfWork CreateRoomFixture(out Guid buildingAId, out Guid buildingBId, out Guid floorA2Id)
    {
        var unitOfWork = new InMemoryUnitOfWork();
        buildingAId = Guid.NewGuid();
        buildingBId = Guid.NewGuid();
        var floorA1Id = Guid.NewGuid();
        floorA2Id = Guid.NewGuid();
        var floorB1Id = Guid.NewGuid();
        unitOfWork.Set<Building>().Items.AddRange(new[]
        {
            new Building { Id = buildingAId, Name = "Building A" },
            new Building { Id = buildingBId, Name = "Building B" }
        });
        unitOfWork.Set<Floor>().Items.AddRange(new[]
        {
            new Floor { Id = floorA1Id, BuildingId = buildingAId, FloorNumber = 1 },
            new Floor { Id = floorA2Id, BuildingId = buildingAId, FloorNumber = 2 },
            new Floor { Id = floorB1Id, BuildingId = buildingBId, FloorNumber = 1 }
        });
        unitOfWork.Set<Room>().Items.AddRange(new[]
        {
            Room(buildingAId, floorA1Id, "101", 500000m, RoomGenderType.Female),
            Room(buildingAId, floorA2Id, "201", 750000m, RoomGenderType.Female),
            Room(buildingAId, floorA2Id, "202", 600000m, RoomGenderType.Female),
            Room(buildingAId, floorA2Id, "203", 550000m, RoomGenderType.Male),
            Room(buildingBId, floorB1Id, "101", 650000m, RoomGenderType.Female)
        });
        return unitOfWork;
    }

    private static Room Room(Guid buildingId, Guid floorId, string roomNumber, decimal monthlyPrice, RoomGenderType genderType) =>
        new()
        {
            Id = Guid.NewGuid(),
            BuildingId = buildingId,
            FloorId = floorId,
            RoomNumber = roomNumber,
            Capacity = 4,
            CurrentOccupancy = 0,
            MonthlyPrice = monthlyPrice,
            Status = RoomStatus.Available,
            GenderType = genderType
        };
}
