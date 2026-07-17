using Xunit;

namespace PrismCoreTests.ServiceHost;

/// <summary>
/// Attaches <see cref="ServiceHostFixture"/> to every test in the "Service Host" collection so all
/// roundtrip tests share one WebApplicationFactory and its model loads.
/// </summary>
[CollectionDefinition("Service Host")]
public class ServiceHostCollection : ICollectionFixture<ServiceHostFixture>
{
}
