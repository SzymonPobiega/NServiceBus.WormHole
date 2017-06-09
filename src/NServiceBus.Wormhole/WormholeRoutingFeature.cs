namespace NServiceBus.Wormhole
{
    using System.Linq;
    using Features;
    using Routing;

    public class WormholeRoutingFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var routingSettings = context.Settings.Get<WormholeRoutingSettings>();
            var unicastRouteTable = context.Settings.Get<UnicastRoutingTable>();

            var route = UnicastRoute.CreateFromPhysicalAddress(routingSettings.GatwayAddress);
            var routeTableEntries = routingSettings.RouteTable.Select(kvp => new RouteTableEntry(kvp.Key, route)).ToList();

            unicastRouteTable.AddOrReplaceRoutes("NServiceBus.Wormhole", routeTableEntries);

            context.Pipeline.Register(new SiteEnricherBehavior(routingSettings.RouteTable), "Adds information about destination site.");
            context.Pipeline.Register(new ReplyBehavior(), "Applies reply behavior.");
        }
    }
}