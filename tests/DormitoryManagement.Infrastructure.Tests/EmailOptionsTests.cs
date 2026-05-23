using DormitoryManagement.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace DormitoryManagement.Infrastructure.Tests;

public sealed class EmailOptionsTests : IDisposable
{
    private readonly string[] _keys =
    {
        "Email__SmtpHost",
        "Email__SmtpPort",
        "Email__SmtpUsername",
        "Email__SmtpPassword",
        "Email__FromAddress",
        "Email__FromName"
    };

    public EmailOptionsTests()
    {
        foreach (var key in _keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void FromConfiguration_prefers_environment_values_over_json_values()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SmtpHost"] = "smtp.gmail.com",
                ["Email:SmtpPort"] = "587",
                ["Email:SmtpUsername"] = "json-user",
                ["Email:SmtpPassword"] = "",
                ["Email:FromAddress"] = "json@example.com",
                ["Email:FromName"] = "Json"
            })
            .Build();
        Environment.SetEnvironmentVariable("Email__SmtpHost", "smtp-relay.brevo.com");
        Environment.SetEnvironmentVariable("Email__SmtpUsername", "env-user");
        Environment.SetEnvironmentVariable("Email__SmtpPassword", "env-secret");
        Environment.SetEnvironmentVariable("Email__FromAddress", "env@example.com");
        Environment.SetEnvironmentVariable("Email__FromName", "Env Name");

        var options = EmailOptions.FromConfiguration(configuration);

        Assert.Equal("smtp-relay.brevo.com", options.SmtpHost);
        Assert.Equal("env-user", options.SmtpUsername);
        Assert.Equal("env-secret", options.SmtpPassword);
        Assert.Equal("env@example.com", options.FromAddress);
        Assert.Equal("Env Name", options.FromName);
    }

    public void Dispose()
    {
        foreach (var key in _keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}
