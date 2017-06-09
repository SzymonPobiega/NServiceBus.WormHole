using NServiceBus.Routing;

namespace NServiceBus.Wormhole.Gateway
{
    class RouteGroup
    {
        public string EndpointName { get; }
        public UnicastRoute[] Routes { get; }

        public RouteGroup(string endpointName, UnicastRoute[] routes)
        {
            EndpointName = endpointName;
            Routes = routes;
        }
    }
}