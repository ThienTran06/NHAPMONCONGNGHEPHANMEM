using System.Text.RegularExpressions;
using DormitoryManagement.Application.Abstractions.Auth;
using DormitoryManagement.Application.Abstractions.Services;
using DormitoryManagement.Application.Common;
using DormitoryManagement.Application.DTOs.Auth;
using DormitoryManagement.Application.Services.Auth;
using DormitoryManagement.Domain.Constants;
using DormitoryManagement.Domain.Entities;
using DormitoryManagement.Domain.Enums;

namespace DormitoryManagement.Application.Tests;

public sealed class AccountRegistrationServiceTests
{
    [Fact]
    public async Task StartStudentAccountRegistrationAsync_saves_pending_and_sends_otp_without_creating_account()
    {
        var fixture = AccountRegistrationFixture.Create();

        var result = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotEqual(Guid.Empty, result.PendingRegistrationId);
        var pending = Assert.Single(fixture.PendingRegistrations.Items);
        Assert.Equal("test.student@ktx.local", pending.Email);
        Assert.Equal("teststudent", pending.Username);
        Assert.Equal("TEST001", pending.StudentCode);
        Assert.NotEqual("123456", pending.PasswordHash);
        Assert.DoesNotContain("123456", pending.PasswordHash, StringComparison.Ordinal);
        Assert.NotEqual(fixture.Email.LastOtpCode, pending.OtpHash);
        Assert.DoesNotContain(fixture.Email.LastOtpCode, pending.OtpHash, StringComparison.Ordinal);
        Assert.Empty(fixture.UnitOfWork.Set<User>().Items);
        Assert.Empty(fixture.UnitOfWork.Set<Student>().Items);
        Assert.Contains("5 minutes", fixture.Email.Messages.Single().BodyText);
    }

    [Fact]
    public async Task VerifyStudentAccountOtpAsync_creates_active_user_and_linked_student_then_deletes_pending()
    {
        var fixture = AccountRegistrationFixture.Create();
        var start = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());

        var result = await fixture.Service.VerifyStudentAccountOtpAsync(start.PendingRegistrationId!.Value, fixture.Email.LastOtpCode);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var user = Assert.Single(fixture.UnitOfWork.Set<User>().Items);
        var student = Assert.Single(fixture.UnitOfWork.Set<Student>().Items);
        Assert.Equal("test.student@ktx.local", user.Email);
        Assert.Equal("teststudent", user.Username);
        Assert.Equal("Nguyen Van Test", user.FullName);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(fixture.StudentRole.Id, user.RoleId);
        Assert.True(fixture.Hasher.VerifyPassword(user.PasswordHash, "123456"));
        Assert.Equal(user.Id, student.UserId);
        Assert.Equal("TEST001", student.StudentCode);
        Assert.Equal(StudentStatus.NotRegistered, student.Status);
        Assert.Empty(fixture.PendingRegistrations.Items);
        Assert.Contains(fixture.Audit.Entries, entry => entry.Action == "RegisterAccount" && entry.EntityName == "User");
        Assert.Contains(fixture.Audit.Entries, entry => entry.Action == "RegisterAccount" && entry.EntityName == "Student");
    }

    [Fact]
    public async Task VerifyStudentAccountOtpAsync_wrong_otp_increments_attempts_and_blocks_after_five()
    {
        var fixture = AccountRegistrationFixture.Create();
        var start = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());

        for (var i = 0; i < 5; i++)
        {
            var wrong = await fixture.Service.VerifyStudentAccountOtpAsync(start.PendingRegistrationId!.Value, "000000");
            Assert.False(wrong.Succeeded);
        }

        var pending = Assert.Single(fixture.PendingRegistrations.Items);
        Assert.Equal(5, pending.AttemptCount);
        var correctAfterBlock = await fixture.Service.VerifyStudentAccountOtpAsync(start.PendingRegistrationId!.Value, fixture.Email.LastOtpCode);
        Assert.False(correctAfterBlock.Succeeded);
        Assert.Contains("Too many", correctAfterBlock.ErrorMessage);
        Assert.Empty(fixture.UnitOfWork.Set<User>().Items);
    }

    [Fact]
    public async Task VerifyStudentAccountOtpAsync_expired_otp_is_rejected_and_cleaned_up()
    {
        var fixture = AccountRegistrationFixture.Create();
        var start = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));

        var result = await fixture.Service.VerifyStudentAccountOtpAsync(start.PendingRegistrationId!.Value, fixture.Email.LastOtpCode);

        Assert.False(result.Succeeded);
        Assert.Contains("expired", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.PendingRegistrations.Items);
        Assert.Empty(fixture.UnitOfWork.Set<User>().Items);
    }

    [Fact]
    public async Task ResendStudentAccountOtpAsync_after_cooldown_replaces_otp_and_invalidates_old_code()
    {
        var fixture = AccountRegistrationFixture.Create();
        var start = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());
        var firstCode = fixture.Email.LastOtpCode;
        fixture.Clock.Advance(TimeSpan.FromSeconds(61));

        var resend = await fixture.Service.ResendStudentAccountOtpAsync(start.PendingRegistrationId!.Value);
        var secondCode = fixture.Email.LastOtpCode;

        Assert.True(resend.Succeeded, resend.ErrorMessage);
        Assert.NotEqual(firstCode, secondCode);
        var oldCodeResult = await fixture.Service.VerifyStudentAccountOtpAsync(start.PendingRegistrationId!.Value, firstCode);
        Assert.False(oldCodeResult.Succeeded);
        var newCodeResult = await fixture.Service.VerifyStudentAccountOtpAsync(start.PendingRegistrationId!.Value, secondCode);
        Assert.True(newCodeResult.Succeeded, newCodeResult.ErrorMessage);
    }

    [Fact]
    public async Task ResendStudentAccountOtpAsync_before_cooldown_is_rejected()
    {
        var fixture = AccountRegistrationFixture.Create();
        var start = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());

        var result = await fixture.Service.ResendStudentAccountOtpAsync(start.PendingRegistrationId!.Value);

        Assert.False(result.Succeeded);
        Assert.Contains("wait", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(fixture.Email.Messages);
    }

    [Fact]
    public async Task StartStudentAccountRegistrationAsync_same_identity_replaces_existing_pending_otp()
    {
        var fixture = AccountRegistrationFixture.Create();
        var first = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());
        var firstCode = fixture.Email.LastOtpCode;
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));

        var second = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());
        var secondCode = fixture.Email.LastOtpCode;

        Assert.True(second.Succeeded, second.ErrorMessage);
        Assert.Equal(first.PendingRegistrationId, second.PendingRegistrationId);
        Assert.Single(fixture.PendingRegistrations.Items);
        Assert.NotEqual(firstCode, secondCode);
        var firstCodeResult = await fixture.Service.VerifyStudentAccountOtpAsync(second.PendingRegistrationId!.Value, firstCode);
        Assert.False(firstCodeResult.Succeeded);
        var secondCodeResult = await fixture.Service.VerifyStudentAccountOtpAsync(second.PendingRegistrationId!.Value, secondCode);
        Assert.True(secondCodeResult.Succeeded, secondCodeResult.ErrorMessage);
    }

    [Fact]
    public async Task StartStudentAccountRegistrationAsync_same_email_can_update_username_and_replace_pending_otp()
    {
        var fixture = AccountRegistrationFixture.Create();
        var first = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());
        var firstCode = fixture.Email.LastOtpCode;
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        var updated = ValidRequest();
        updated.Username = "UpdatedUserName";

        var second = await fixture.Service.StartStudentAccountRegistrationAsync(updated);
        var secondCode = fixture.Email.LastOtpCode;

        Assert.True(second.Succeeded, second.ErrorMessage);
        Assert.Equal(first.PendingRegistrationId, second.PendingRegistrationId);
        var pending = Assert.Single(fixture.PendingRegistrations.Items);
        Assert.Equal("updatedusername", pending.Username);
        Assert.NotEqual(firstCode, secondCode);
        var secondCodeResult = await fixture.Service.VerifyStudentAccountOtpAsync(second.PendingRegistrationId!.Value, secondCode);
        Assert.True(secondCodeResult.Succeeded, secondCodeResult.ErrorMessage);
        Assert.Equal("updatedusername", fixture.UnitOfWork.Set<User>().Items.Single().Username);
    }

    [Fact]
    public async Task StartStudentAccountRegistrationAsync_removes_expired_pending_before_creating_replacement()
    {
        var fixture = AccountRegistrationFixture.Create();
        await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));

        var second = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());

        Assert.True(second.Succeeded, second.ErrorMessage);
        Assert.Single(fixture.PendingRegistrations.Items);
        Assert.Equal(2, fixture.Email.Messages.Count);
    }

    [Fact]
    public async Task VerifyStudentAccountOtpAsync_second_verify_for_same_pending_id_does_not_create_duplicate_account()
    {
        var fixture = AccountRegistrationFixture.Create();
        var start = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());
        var otp = fixture.Email.LastOtpCode;

        var first = await fixture.Service.VerifyStudentAccountOtpAsync(start.PendingRegistrationId!.Value, otp);
        var second = await fixture.Service.VerifyStudentAccountOtpAsync(start.PendingRegistrationId!.Value, otp);

        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.False(second.Succeeded);
        Assert.Single(fixture.UnitOfWork.Set<User>().Items);
        Assert.Single(fixture.UnitOfWork.Set<Student>().Items);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("username")]
    [InlineData("studentCode")]
    public async Task StartStudentAccountRegistrationAsync_rejects_existing_duplicates(string duplicate)
    {
        var fixture = AccountRegistrationFixture.Create();
        if (duplicate == "email")
        {
            fixture.UnitOfWork.Set<User>().Items.Add(new User { Id = Guid.NewGuid(), Email = "test.student@ktx.local", Username = "other", FullName = "Other" });
        }
        else if (duplicate == "username")
        {
            fixture.UnitOfWork.Set<User>().Items.Add(new User { Id = Guid.NewGuid(), Email = "other@ktx.local", Username = "teststudent", FullName = "Other" });
        }
        else
        {
            fixture.UnitOfWork.Set<Student>().Items.Add(new Student { Id = Guid.NewGuid(), StudentCode = "TEST001", FullName = "Other" });
        }

        var result = await fixture.Service.StartStudentAccountRegistrationAsync(ValidRequest());

        Assert.False(result.Succeeded);
        Assert.Contains(duplicate == "studentCode" ? "Student code" : duplicate == "email" ? "Email" : "Username", result.ErrorMessage);
        Assert.Empty(fixture.PendingRegistrations.Items);
        Assert.Empty(fixture.Email.Messages);
    }

    [Fact]
    public async Task Newly_registered_student_can_login_with_email_username_or_student_code_after_otp_verification()
    {
        var fixture = AccountRegistrationFixture.Create();
        var request = ValidRequest();
        var start = await fixture.Service.StartStudentAccountRegistrationAsync(request);
        var registration = await fixture.Service.VerifyStudentAccountOtpAsync(start.PendingRegistrationId!.Value, fixture.Email.LastOtpCode);
        Assert.True(registration.Succeeded);

        foreach (var login in new[] { request.Email, request.Username, request.StudentCode })
        {
            var session = new TestSessionService();
            var authService = new AuthService(
                fixture.Users,
                fixture.Hasher,
                session,
                session,
                fixture.UnitOfWork,
                fixture.Audit,
                fixture.Clock,
                new SecurityOptions());

            var loginResult = await authService.LoginAsync(new LoginRequest
            {
                EmailOrUsernameOrStudentCode = login,
                Password = request.Password
            });

            Assert.True(loginResult.Succeeded, loginResult.ErrorMessage);
            Assert.Equal(RoleNames.Student, loginResult.User?.RoleName);
            Assert.Equal(registration.StudentId, loginResult.User?.StudentId);
        }
    }

    private static RegisterAccountRequest ValidRequest() =>
        new()
        {
            FullName = "Nguyen Van Test",
            Email = "Test.Student@KTX.Local ",
            Username = "TestStudent",
            StudentCode = "test001",
            PhoneNumber = "0900000000",
            Gender = "Male",
            DateOfBirth = new DateTime(2004, 1, 1),
            Password = "123456",
            ConfirmPassword = "123456"
        };

    private sealed class AccountRegistrationFixture
    {
        private AccountRegistrationFixture()
        {
            UnitOfWork = new InMemoryUnitOfWork();
            StudentRole = new Role { Id = Guid.NewGuid(), Name = RoleNames.Student };
            UnitOfWork.Set<Role>().Items.Add(StudentRole);
            Users = new InMemoryUserRepository(UnitOfWork);
            Students = new InMemoryStudentRepository(UnitOfWork);
            PendingRegistrations = new InMemoryPendingAccountRegistrationRepository(UnitOfWork);
            Hasher = new TestPasswordHasher();
            Email = new RecordingEmailSender();
            Audit = new RecordingAuditLogService();
            Clock = new MutableDateTimeProvider(new DateTime(2026, 5, 14, 8, 0, 0, DateTimeKind.Utc));
            OtpGenerator = new SequenceOtpGenerator();
            Service = new AccountRegistrationService(
                Users,
                Students,
                PendingRegistrations,
                UnitOfWork,
                Hasher,
                Audit,
                Email,
                Clock,
                OtpGenerator);
        }

        public InMemoryUnitOfWork UnitOfWork { get; }
        public Role StudentRole { get; }
        public InMemoryUserRepository Users { get; }
        public InMemoryStudentRepository Students { get; }
        public InMemoryPendingAccountRegistrationRepository PendingRegistrations { get; }
        public TestPasswordHasher Hasher { get; }
        public RecordingEmailSender Email { get; }
        public RecordingAuditLogService Audit { get; }
        public MutableDateTimeProvider Clock { get; }
        public SequenceOtpGenerator OtpGenerator { get; }
        public AccountRegistrationService Service { get; }

        public static AccountRegistrationFixture Create() => new();
    }

    private sealed class MutableDateTimeProvider : IDateTimeProvider
    {
        public MutableDateTimeProvider(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; private set; }
        public void Advance(TimeSpan value) => UtcNow = UtcNow.Add(value);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        private static readonly Regex OtpRegex = new(@"\b\d{6}\b", RegexOptions.Compiled);

        public List<EmailMessage> Messages { get; } = new();
        public string LastOtpCode => OtpRegex.Match(Messages.Last().BodyText).Value;

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceOtpGenerator : IOtpGenerator
    {
        private int _next = 654321;

        public string GenerateCode() => (_next++).ToString("D6");
    }

    private sealed class TestSessionService : ISessionService, ICurrentUserService
    {
        public CurrentUserDto? CurrentUser { get; private set; }
        public Guid? UserId => CurrentUser?.UserId;
        public string? UserName => CurrentUser?.Username;
        public string? Email => CurrentUser?.Email;
        public string? FullName => CurrentUser?.FullName;
        public IReadOnlyCollection<string> Roles => CurrentUser?.RoleName is { Length: > 0 } roleName ? new[] { roleName } : Array.Empty<string>();
        public bool IsAuthenticated => CurrentUser is not null;
        public void SetCurrentUser(CurrentUserDto user) => CurrentUser = user;
        public void Clear() => CurrentUser = null;
        public bool IsInRole(string roleName) => Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }
}
