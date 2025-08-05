using Xunit;

namespace Scanner111.Tests.TestHelpers;

/// <summary>
/// Test collection to ensure settings-related tests run sequentially to avoid
/// environment variable conflicts.
/// </summary>
[CollectionDefinition("Settings Tests")]
public class SettingsTestCollection : ICollectionFixture<object>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}