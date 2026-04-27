using MarkBackend.Tests.Infrastructure;

namespace MarkBackend.Tests.Infrastructure;

[Collection("Database collection")]
public abstract class IntegrationTestBase
{
    protected readonly HttpClient Client;
    protected readonly CustomWebApplicationFactory Factory;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }
}
