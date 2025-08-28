using Xunit;

// Configure parallel test execution at assembly level
[assembly: CollectionBehavior(
    DisableTestParallelization = false,
    MaxParallelThreads = 4)]

// Mark this as the Scanner111 test suite
[assembly: AssemblyTrait("TestSuite", "Scanner111")]
[assembly: AssemblyTrait("Version", "1.0")]

// Configure test output
[assembly: AssemblyTrait("OutputDetail", "Verbose")]