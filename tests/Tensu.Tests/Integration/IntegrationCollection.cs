using Xunit;

namespace Tensu.Tests.Integration;

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestBase>
{
}
