using MarkBackend.Helpers;
using Microsoft.Extensions.Configuration;

namespace MarkBackend.Tests.Infrastructure;

/// <summary>
/// Mock EmailHelper that doesn't actually send emails during tests
/// </summary>
public class MockEmailHelper : EmailHelper
{
    public MockEmailHelper(IConfiguration configuration) : base(configuration)
    {
        // Use the provided configuration
    }

    public new Task<bool> SendEmailRegistrationConfirm(string email, string confirmationLink)
    {
        // Always return success without actually sending email
        return Task.FromResult(true);
    }

    public new Task<bool> SendEmailPasswordReset(string email, string resetLink)
    {
        // Always return success without actually sending email
        return Task.FromResult(true);
    }
}
