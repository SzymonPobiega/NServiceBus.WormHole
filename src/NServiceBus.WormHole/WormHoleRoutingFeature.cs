using System.Linq;
using NServiceBus.Features;
using NServiceBus.Routing;

namespace NServiceBus.WormHole
{
    public class WormHoleRoutingFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var routingSettings = context.Settings.Get<WormHoleRoutingSettings>();
            var unicastRouteTable = context.Settings.Get<UnicastRoutingTable>();

            var route = UnicastRoute.CreateFromPhysicalAddress(routingSettings.GatwayAddress);
            var routeTableEntries = routingSettings.RouteTable.Select(kvp => new RouteTableEntry(kvp.Key, route)).ToList();

            unicastRouteTable.AddOrReplaceRoutes("NServiceBus.WormHole", routeTableEntries);

            context.Pipeline.Register(new SiteEnricherBehavior(routingSettings.RouteTable), "Adds information about destination site.");
        }
    }
}