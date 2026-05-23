using DormitoryManagement.Application.Abstractions.Auth;
using DormitoryManagement.Application.Abstractions.Data;
using DormitoryManagement.Application.Abstractions.Services;
using DormitoryManagement.Application.DTOs.Forum;
using DormitoryManagement.Application.Security;
using DormitoryManagement.Application.Services.Forum;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class ForumServiceTests
{
    [Fact]
    public async Task CreateThreadAsync_persists_thread_for_visible_topic()
    {
        var context = CreateContext(RoleNames.Student);
        var topic = context.SeedTopic("Announcements");

        var thread = await context.Service.CreateThreadAsync(new CreateForumThreadRequest
        {
            TopicId = topic.Id,
            Title = "Quiet hours",
            Content = "Please keep hallways quiet after 10 PM."
        });

        var stored = Assert.Single(context.UnitOfWork.Set<ForumThread>().Items);
        Assert.Equal(topic.Id, stored.TopicId);
        Assert.Equal(context.CurrentUser.UserId, stored.UserId);
        Assert.Equal("Quiet hours", thread.Title);
        Assert.Equal("Please keep hallways quiet after 10 PM.", thread.Content);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task GetThreadsAsync_returns_newest_first_visible_threads_with_search_and_paging()
    {
        var context = CreateContext(RoleNames.Student);
        var topic = context.SeedTopic("Maintenance");
        context.SeedThread(topic, "Old plumbing", "Sink drain is slow.", createdAt: DateTime.UtcNow.AddHours(-3));
        var newest = context.SeedThread(topic, "New plumbing", "Shower pressure is low.", createdAt: DateTime.UtcNow.AddHours(-1));
        context.SeedThread(topic, "Hidden plumbing", "Hidden row", ForumContentStatus.Hidden, DateTime.UtcNow);
        context.SeedThread(topic, "Laundry", "Dryer question.", createdAt: DateTime.UtcNow.AddHours(-2));

        var page = await context.Service.GetThreadsAsync(new ForumThreadQuery
        {
            TopicId = topic.Id,
            SearchText = "plumbing",
            Page = 1,
            PageSize = 1
        });

        var row = Assert.Single(page.Items);
        Assert.Equal(newest.Id, row.Id);
        Assert.Equal(2, page.TotalCount);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task GetThreadDetailAsync_loads_five_comment_levels_and_marks_deeper_replies()
    {
        var context = CreateContext(RoleNames.Student);
        var topic = context.SeedTopic("General");
        var thread = context.SeedThread(topic, "Nested", "Nested replies");
        var level0 = context.SeedComment(thread, "Level 0");
        var level1 = context.SeedComment(thread, "Level 1", level0);
        var level2 = context.SeedComment(thread, "Level 2", level1);
        var level3 = context.SeedComment(thread, "Level 3", level2);
        var level4 = context.SeedComment(thread, "Level 4", level3);
        context.SeedComment(thread, "Level 5", level4);

        var detail = await context.Service.GetThreadDetailAsync(thread.Id, commentDepth: 5);

        Assert.NotNull(detail);
        var dto0 = Assert.Single(detail!.Comments);
        Assert.Equal(0, dto0.Depth);
        Assert.Equal(1, dto0.Replies[0].Depth);
        Assert.Equal(2, dto0.Replies[0].Replies[0].Depth);
        Assert.Equal(3, dto0.Replies[0].Replies[0].Replies[0].Depth);
        var dto4 = Assert.Single(dto0.Replies[0].Replies[0].Replies[0].Replies);
        Assert.Equal("Level 4", dto4.Content);
        Assert.True(dto4.HasMoreReplies);
        Assert.Empty(dto4.Replies);
    }

    [Fact]
    public async Task GetCommentRepliesAsync_loads_more_replies_for_parent()
    {
        var context = CreateContext(RoleNames.Student);
        var topic = context.SeedTopic("General");
        var thread = context.SeedThread(topic, "Nested", "Nested replies");
        var parent = context.SeedComment(thread, "Parent");
        var child = context.SeedComment(thread, "Child", parent);

        var replies = await context.Service.GetCommentRepliesAsync(parent.Id, depth: 5);

        var reply = Assert.Single(replies);
        Assert.Equal(child.Id, reply.Id);
        Assert.Equal(1, reply.Depth);
    }

    [Fact]
    public async Task ToggleThreadLikeAsync_likes_and_unlikes_thread()
    {
        var context = CreateContext(RoleNames.Student);
        var topic = context.SeedTopic("General");
        var thread = context.SeedThread(topic, "Welcome", "Hello");

        var liked = await context.Service.ToggleThreadLikeAsync(thread.Id);
        var unliked = await context.Service.ToggleThreadLikeAsync(thread.Id);

        Assert.True(liked.IsLikedByCurrentUser);
        Assert.Equal(1, liked.LikeCount);
        Assert.False(unliked.IsLikedByCurrentUser);
        Assert.Equal(0, unliked.LikeCount);
    }

    [Fact]
    public async Task ToggleCommentLikeAsync_rejects_hidden_comment()
    {
        var context = CreateContext(RoleNames.Student);
        var topic = context.SeedTopic("General");
        var thread = context.SeedThread(topic, "Welcome", "Hello");
        var comment = context.SeedComment(thread, "Hidden", status: ForumContentStatus.Hidden);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.ToggleCommentLikeAsync(comment.Id));
    }

    [Fact]
    public async Task CreateCommentAsync_notifies_thread_author_parent_author_and_unique_mentions()
    {
        var context = CreateContext(RoleNames.Student);
        var topic = context.SeedTopic("General");
        var threadAuthor = context.SeedUser("thread.owner");
        var parentAuthor = context.SeedUser("parent.owner");
        var mentioned = context.SeedUser("staff.user");
        var ignoredUrlUser = context.SeedUser("skip.user");
        var ignoredEmailUser = context.SeedUser("domain.local");
        var thread = context.SeedThread(topic, "Welcome", "Hello", user: threadAuthor);
        var parent = context.SeedComment(thread, "Parent", user: parentAuthor);

        await context.Service.CreateCommentAsync(new CreateForumCommentRequest
        {
            ThreadId = thread.Id,
            ParentCommentId = parent.Id,
            Content = "Thanks @staff.user @staff.user @unknown hi user@domain.local https://dorm.local/@skip.user"
        });

        Assert.Contains(context.Notifications.Entries, entry => entry.UserId == parentAuthor.Id && entry.Title == "Forum reply");
        Assert.Contains(context.Notifications.Entries, entry => entry.UserId == mentioned.Id && entry.Title == "Forum mention");
        Assert.DoesNotContain(context.Notifications.Entries, entry => entry.UserId == threadAuthor.Id);
        Assert.DoesNotContain(context.Notifications.Entries, entry => entry.UserId == ignoredUrlUser.Id);
        Assert.DoesNotContain(context.Notifications.Entries, entry => entry.UserId == ignoredEmailUser.Id);
        Assert.Equal(2, context.Notifications.Entries.Count);
    }

    [Fact]
    public async Task CreateCommentAsync_rejects_locked_threads()
    {
        var context = CreateContext(RoleNames.Student);
        var topic = context.SeedTopic("General");
        var thread = context.SeedThread(topic, "Locked", "No replies", ForumContentStatus.Locked);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.CreateCommentAsync(new CreateForumCommentRequest
        {
            ThreadId = thread.Id,
            Content = "Can I reply?"
        }));
    }

    [Fact]
    public async Task Moderation_requires_permission_updates_status_and_writes_audit()
    {
        var studentContext = CreateContext(RoleNames.Student);
        var topic = studentContext.SeedTopic("General");
        var thread = studentContext.SeedThread(topic, "Moderate me", "Body");

        await Assert.ThrowsAsync<AccessDeniedException>(() => studentContext.Service.HideThreadAsync(new ForumModerationRequest
        {
            ContentId = thread.Id,
            Reason = "Off topic"
        }));

        var staffContext = studentContext.WithRole(RoleNames.Staff);
        await staffContext.Service.HideThreadAsync(new ForumModerationRequest { ContentId = thread.Id, Reason = "Off topic" });
        await staffContext.Service.LockThreadAsync(new ForumModerationRequest { ContentId = thread.Id, Reason = "Pause replies" });

        Assert.Equal(ForumContentStatus.Locked, thread.Status);
        Assert.Contains(staffContext.AuditLog.Entries, entry => entry.Action == "Forum.ThreadHidden" && entry.EntityId == thread.Id);
        Assert.Contains(staffContext.AuditLog.Entries, entry => entry.Action == "Forum.ThreadLocked" && entry.EntityId == thread.Id);
    }

    private static ForumTestContext CreateContext(string roleName)
    {
        var unitOfWork = new InMemoryUnitOfWork();
        var user = new TestCurrentUser(roleName);
        unitOfWork.Set<User>().Items.Add(new User
        {
            Id = user.UserId!.Value,
            Username = user.UserName ?? roleName,
            FullName = user.FullName ?? roleName,
            Email = user.Email ?? $"{roleName}@ktx.local"
        });

        var audit = new RecordingAuditLogService();
        var notifications = new RecordingNotificationService();
        return new ForumTestContext(unitOfWork, user, audit, notifications);
    }

    private sealed class ForumTestContext
    {
        public ForumTestContext(
            InMemoryUnitOfWork unitOfWork,
            TestCurrentUser currentUser,
            RecordingAuditLogService auditLog,
            RecordingNotificationService notifications)
        {
            UnitOfWork = unitOfWork;
            CurrentUser = currentUser;
            AuditLog = auditLog;
            Notifications = notifications;
            Service = CreateService(currentUser);
        }

        public InMemoryUnitOfWork UnitOfWork { get; }
        public TestCurrentUser CurrentUser { get; }
        public RecordingAuditLogService AuditLog { get; }
        public RecordingNotificationService Notifications { get; }
        public ForumService Service { get; private set; }

        public ForumTestContext WithRole(string roleName)
        {
            var user = new TestCurrentUser(roleName);
            UnitOfWork.Set<User>().Items.Add(new User
            {
                Id = user.UserId!.Value,
                Username = user.UserName ?? roleName,
                FullName = user.FullName ?? roleName,
                Email = user.Email ?? $"{roleName}@ktx.local"
            });
            return new ForumTestContext(UnitOfWork, user, AuditLog, Notifications);
        }

        public ForumTopic SeedTopic(string title, ForumContentStatus status = ForumContentStatus.Visible)
        {
            var topic = new ForumTopic
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = $"{title} description",
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            UnitOfWork.Set<ForumTopic>().Items.Add(topic);
            return topic;
        }

        public User SeedUser(string username)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                FullName = username.Replace('.', ' '),
                Email = $"{username}@ktx.local"
            };
            UnitOfWork.Set<User>().Items.Add(user);
            return user;
        }

        public ForumThread SeedThread(
            ForumTopic topic,
            string title,
            string content,
            ForumContentStatus status = ForumContentStatus.Visible,
            DateTime? createdAt = null,
            User? user = null)
        {
            var author = user ?? UnitOfWork.Set<User>().Items.First(row => row.Id == CurrentUser.UserId);
            var thread = new ForumThread
            {
                Id = Guid.NewGuid(),
                TopicId = topic.Id,
                Topic = topic,
                UserId = author.Id,
                User = author,
                Title = title,
                Content = content,
                Status = status,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
            UnitOfWork.Set<ForumThread>().Items.Add(thread);
            topic.Threads.Add(thread);
            return thread;
        }

        public ForumComment SeedComment(
            ForumThread thread,
            string content,
            ForumComment? parent = null,
            ForumContentStatus status = ForumContentStatus.Visible,
            User? user = null)
        {
            var author = user ?? UnitOfWork.Set<User>().Items.First(row => row.Id == CurrentUser.UserId);
            var comment = new ForumComment
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                Thread = thread,
                ParentCommentId = parent?.Id,
                ParentComment = parent,
                UserId = author.Id,
                User = author,
                Content = content,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            UnitOfWork.Set<ForumComment>().Items.Add(comment);
            thread.Comments.Add(comment);
            parent?.Replies.Add(comment);
            return comment;
        }

        private ForumService CreateService(ICurrentUserService currentUser) =>
            new(
                new PermissionService(currentUser),
                UnitOfWork,
                AuditLog,
                currentUser,
                Notifications);
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<(Guid UserId, string Title, string Message)> Entries { get; } = new();

        public Task NotifyUserAsync(Guid userId, string title, string message, CancellationToken ct = default)
        {
            Entries.Add((userId, title, message));
            return Task.CompletedTask;
        }

        public Task NotifyRoleAsync(string roleName, string title, string message, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Application.DTOs.Notifications.NotificationDto>> GetCurrentUserNotificationsAsync(int take = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Application.DTOs.Notifications.NotificationDto>>(Array.Empty<Application.DTOs.Notifications.NotificationDto>());

        public Task<int> GetCurrentUserUnreadCountAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task MarkAsReadAsync(Guid userNotificationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task MarkAllCurrentUserNotificationsAsReadAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
