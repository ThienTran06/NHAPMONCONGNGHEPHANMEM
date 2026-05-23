using DormitoryManagement.Application.Abstractions.Services;
using DormitoryManagement.Infrastructure.Services;

namespace DormitoryManagement.Infrastructure.Tests;

public sealed class MailKitEmailSenderTests
{
    [Fact]
    public async Task SendAsync_without_smtp_password_fails_before_network_connection()
    {
        var sender = new MailKitEmailSender(new EmailOptions
        {
            SmtpHost = "smtp.gmail.com",
            SmtpPort = 587,
            SmtpUsername = "sender@gmail.com",
            SmtpPassword = "",
            FromAddress = "sender@gmail.com",
            FromName = "DormitoryManagement"
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new EmailMessage
            {
                To = "student@ktx.local",
                Subject = "Test",
                BodyText = "123456",
                BodyHtml = "<p>123456</p>"
            }));

        Assert.Contains("Email:SmtpPassword", ex.Message);
    }
}
