using Xunit;

namespace Scanner111.Test.Infrastructure.Fixtures;

/// <summary>
/// Collection definition for tests that need shared temporary directory management.
/// Tests in this collection will share the same TempDirectoryFixture instance.
/// </summary>
[CollectionDefinition("TempDirectory")]
public class TempDirectoryCollection : ICollectionFixture<TempDirectoryFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition]
    // and the ICollectionFixture<> interface.
}