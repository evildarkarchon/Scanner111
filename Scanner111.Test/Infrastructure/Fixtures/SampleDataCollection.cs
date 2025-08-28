using Xunit;

namespace Scanner111.Test.Infrastructure.Fixtures;

/// <summary>
/// Collection definition for tests that need cached sample data access.
/// Tests in this collection will share the same SampleDataCacheFixture instance,
/// reducing I/O operations and improving test performance.
/// </summary>
[CollectionDefinition("SampleData")]
public class SampleDataCollection : ICollectionFixture<SampleDataCacheFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition]
    // and the ICollectionFixture<> interface.
}

/// <summary>
/// Collection definition that combines temp directory and sample data fixtures.
/// Use this for integration tests that need both capabilities.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : 
    ICollectionFixture<TempDirectoryFixture>,
    ICollectionFixture<SampleDataCacheFixture>
{
    // This class has no code, and is never created.
}