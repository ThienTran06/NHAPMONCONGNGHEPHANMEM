using DormitoryManagement.Application.Services.Students;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class StudentServiceTests
{
    [Fact]
    public async Task GetStudentsAsync_returns_existing_non_deleted_students()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var studentOne = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SV002",
            FullName = "Tran Thi Binh",
            Email = "binh@ktx.local",
            PhoneNumber = "0900000002",
            Status = StudentStatus.Staying
        };
        var studentTwo = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SV001",
            FullName = "Nguyen Van An",
            Email = "an@ktx.local",
            PhoneNumber = "0900000001",
            Status = StudentStatus.Pending
        };
        unitOfWork.Set<Student>().Items.AddRange(new[]
        {
            studentOne,
            studentTwo,
            new Student { Id = Guid.NewGuid(), StudentCode = "SV999", FullName = "Deleted", IsDeleted = true }
        });
        var service = new StudentService(
            new InMemoryStudentRepository(unitOfWork),
            new AllowAllPermissionService(),
            unitOfWork,
            new TestCurrentUser(RoleNames.Manager));

        var result = await service.GetStudentsAsync();

        Assert.Equal(2, result.TotalCount);
        Assert.Collection(result.Items,
            student => Assert.Equal("SV001", student.StudentCode),
            student => Assert.Equal("SV002", student.StudentCode));
        Assert.Equal(StudentStatus.Pending, result.Items[0].Status);
        Assert.Equal("0900000002", result.Items[1].PhoneNumber);
    }

    [Fact]
    public async Task GetStudentsAsync_scopes_building_manager_to_assigned_building()
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var buildingAId = Guid.NewGuid();
        var buildingBId = Guid.NewGuid();
        var roomAId = Guid.NewGuid();
        var roomBId = Guid.NewGuid();
        var studentA = new Student { Id = Guid.NewGuid(), StudentCode = "SV001", FullName = "Building A Student" };
        var studentB = new Student { Id = Guid.NewGuid(), StudentCode = "SV002", FullName = "Building B Student" };
        unitOfWork.Set<Room>().Items.AddRange(new[]
        {
            new Room { Id = roomAId, BuildingId = buildingAId, RoomNumber = "A-101" },
            new Room { Id = roomBId, BuildingId = buildingBId, RoomNumber = "B-101" }
        });
        unitOfWork.Set<Student>().Items.AddRange(new[] { studentA, studentB });
        unitOfWork.Set<RoomAssignment>().Items.AddRange(new[]
        {
            new RoomAssignment { Id = Guid.NewGuid(), StudentId = studentA.Id, RoomId = roomAId, IsActive = true },
            new RoomAssignment { Id = Guid.NewGuid(), StudentId = studentB.Id, RoomId = roomBId, IsActive = true }
        });
        var service = new StudentService(
            new InMemoryStudentRepository(unitOfWork),
            new AllowAllPermissionService(),
            unitOfWork,
            new TestCurrentUser(RoleNames.BuildingManager, buildingId: buildingAId));

        var result = await service.GetStudentsAsync();

        var student = Assert.Single(result.Items);
        Assert.Equal("SV001", student.StudentCode);
    }
}
