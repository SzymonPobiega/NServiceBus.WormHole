using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transports.Http;

public class ConfigureEndpointHttpTransport : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var transportConfig = configuration.UseTransport<HttpTransport>();
        var routingConfig = transportConfig.Routing();

        foreach (var publisher in publisherMetadata.Publishers)
        {
            foreach (var eventType in publisher.Events)
            {
                routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
            }
        }

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }
}