using DormitoryManagement.Application.Security;
using DormitoryManagement.Domain.Constants;

namespace DormitoryManagement.Application.Tests;

public sealed class PermissionServiceTests
{
    [Theory]
    [InlineData(RoleNames.Manager)]
    [InlineData(RoleNames.BuildingManager)]
    public async Task HasPermissionAsync_allows_managers_to_read_students(string roleName)
    {
        var service = new PermissionService(new TestCurrentUser(roleName));

        Assert.True(await service.HasPermissionAsync(PermissionNames.StudentsRead));
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Manager)]
    [InlineData(RoleNames.BuildingManager)]
    [InlineData(RoleNames.Staff)]
    [InlineData(RoleNames.Student)]
    public async Task HasPermissionAsync_allows_authenticated_roles_to_use_forum(string roleName)
    {
        var service = new PermissionService(new TestCurrentUser(roleName));

        Assert.True(await service.HasPermissionAsync(PermissionNames.ForumRead));
        Assert.True(await service.HasPermissionAsync(PermissionNames.ForumThreadCreate));
        Assert.True(await service.HasPermissionAsync(PermissionNames.ForumCommentCreate));
        Assert.True(await service.HasPermissionAsync(PermissionNames.ForumLike));
    }

    [Fact]
    public async Task HasPermissionAsync_denies_student_forum_moderation()
    {
        var service = new PermissionService(new TestCurrentUser(RoleNames.Student));

        Assert.False(await service.HasPermissionAsync(PermissionNames.ForumModerate));
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Manager)]
    [InlineData(RoleNames.BuildingManager)]
    [InlineData(RoleNames.Staff)]
    public async Task HasPermissionAsync_allows_staff_roles_forum_moderation(string roleName)
    {
        var service = new PermissionService(new TestCurrentUser(roleName));

        Assert.True(await service.HasPermissionAsync(PermissionNames.ForumModerate));
    }
}
