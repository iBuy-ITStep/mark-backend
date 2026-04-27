namespace MarkBackend.Tests.Infrastructure;

/// <summary>
/// Collection definition to ensure all tests using the shared database run sequentially
/// </summary>
[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
