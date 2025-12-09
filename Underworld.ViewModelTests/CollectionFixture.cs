using Xunit;

namespace Underworld.ViewModelTests;

/// <summary>
/// Disables parallel test execution for all tests in this assembly.
/// This is necessary because tests share the static MainWindowViewModel.AllWads collection.
/// </summary>
[CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
public class NonParallelCollectionDefinition
{
}
