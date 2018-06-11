using System;
using System.Collections.Generic;
using System.Linq;

namespace NServiceBus.Wormhole.Gateway
{
    using Routing;

    class MessageRouter
    {
        RoutingTable routingTable;
        EndpointInstances endpointInstances;
        DistributionPolicy distributionPolicy;

        public MessageRouter(RoutingTable routingTable, EndpointInstances endpointInstances, DistributionPolicy distributionPolicy)
        {
            this.routingTable = routingTable;
            this.endpointInstances = endpointInstances;
            this.distributionPolicy = distributionPolicy;
        }

        public string[] Route(MessageType messageType, Func<EndpointInstance, string> resolveTransportAddress)
        {
            var routes = routingTable.GetRoutesFor(messageType);
            var selectedDestinations = SelectDestinationsForEachEndpoint(routes, resolveTransportAddress);
            return selectedDestinations.ToArray();
        }

        HashSet<string> SelectDestinationsForEachEndpoint(IEnumerable<RouteGroup> routeGroups, Func<EndpointInstance, string> resolveTransportAddress)
        {
            //Make sure we are sending only one to each transport destination. Might happen when there are multiple routing information sources.
            var addresses = new HashSet<string>();

            foreach (var group in routeGroups)
            {
                if (group.EndpointName == null) //Routing targets that do not specify endpoint name
                {
                    //Send a message to each target as we have no idea which endpoint they represent
                    foreach (var subscriber in group.Routes)
                    {
                        foreach (var address in ResolveRoute(subscriber, resolveTransportAddress))
                        {
                            addresses.Add(address);
                        }
                    }
                }
                else
                {
                    var candidates = group.Routes.SelectMany(x => ResolveRoute(x, resolveTransportAddress)).ToArray();
                    var selected = distributionPolicy.GetDistributionStrategy(group.EndpointName, DistributionStrategyScope.Publish).SelectDestination(candidates);
                    addresses.Add(selected);
                }
            }

            return addresses;
        }

        IEnumerable<string> ResolveRoute(UnicastRoute route, Func<EndpointInstance, string> resolveTransportAddress)
        {
            if (route.Instance != null)
            {
                yield return resolveTransportAddress(route.Instance);
            }
            else if (route.PhysicalAddress != null)
            {
                yield return route.PhysicalAddress;
            }
            else
            {
                foreach (var instance in endpointInstances.FindInstances(route.Endpoint))
                {
                    yield return resolveTransportAddress(instance);
                }
            }
        }
    }
}