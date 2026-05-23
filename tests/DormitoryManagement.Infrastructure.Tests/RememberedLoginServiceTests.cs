using DormitoryManagement.Application.DTOs.Auth;
using DormitoryManagement.Infrastructure.Services;

namespace DormitoryManagement.Infrastructure.Tests;

public sealed class RememberedLoginServiceTests
{
    [Fact]
    public void SaveFullLogin_StoresProtectedPasswordAndLoadsIt()
    {
        using var temp = new TempRememberedLoginFile();
        var service = new RememberedLoginService(temp.Path, new FakeStringProtector());

        service.SaveFullLogin("student@ktx.local", "P@ssw0rd!");

        var fileText = File.ReadAllText(temp.Path);
        Assert.Contains("student@ktx.local", fileText);
        Assert.DoesNotContain("P@ssw0rd!", fileText, StringComparison.Ordinal);

        var remembered = service.Load();
        Assert.Equal("student@ktx.local", remembered.EmailOrStudentCode);
        Assert.Equal("P@ssw0rd!", remembered.Password);
        Assert.True(remembered.HasPassword);
    }

    [Fact]
    public void SaveEmailOnly_RemovesRememberedPassword()
    {
        using var temp = new TempRememberedLoginFile();
        var service = new RememberedLoginService(temp.Path, new FakeStringProtector());

        service.SaveFullLogin("student@ktx.local", "P@ssw0rd!");
        service.SaveEmailOnly("student@ktx.local");

        var remembered = service.Load();
        Assert.Equal("student@ktx.local", remembered.EmailOrStudentCode);
        Assert.Equal(string.Empty, remembered.Password);
        Assert.False(remembered.HasPassword);
    }

    [Fact]
    public void Clear_RemovesRememberedLogin()
    {
        using var temp = new TempRememberedLoginFile();
        var service = new RememberedLoginService(temp.Path, new FakeStringProtector());

        service.SaveFullLogin("student@ktx.local", "P@ssw0rd!");
        service.Clear();

        Assert.False(File.Exists(temp.Path));
        Assert.Equal(RememberedLoginState.Empty, service.Load());
    }

    private sealed class FakeStringProtector : IStringProtector
    {
        public string Protect(string value) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"protected::{value}"));

        public string Unprotect(string protectedValue)
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue));
            return decoded.StartsWith("protected::", StringComparison.Ordinal)
                ? decoded["protected::".Length..]
                : string.Empty;
        }
    }

    private sealed class TempRememberedLoginFile : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dormitory-remember-{Guid.NewGuid():N}");

        public TempRememberedLoginFile()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "remembered-login.json");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
