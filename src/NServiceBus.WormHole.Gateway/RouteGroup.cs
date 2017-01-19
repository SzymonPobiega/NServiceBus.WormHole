using NServiceBus.Routing;

namespace NServiceBus.WormHole.Gateway
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