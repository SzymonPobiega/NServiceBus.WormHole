using System;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Features;

namespace NServiceBus.WormHole
{
    public static class WormHoleRoutingExtensions
    {
        /// <summary>
        /// Enables the Worm Hole gateway.
        /// </summary>
        /// <param name="config">Endpoint config.</param>
        /// <param name="gatewayAddress">Worm Hole gateway address.</param>
        public static WormHoleRoutingSettings UseWormHoleGateway(this EndpointConfiguration config, string gatewayAddress)
        {
            if (gatewayAddress == null)
            {
                throw new ArgumentNullException(nameof(gatewayAddress));
            }
            var settings = config.GetSettings();
            settings.EnableFeatureByDefault<WormHoleRoutingFeature>();
            WormHoleRoutingSettings routingSettings;
            if (!settings.TryGet(out routingSettings))
            {
                routingSettings = new WormHoleRoutingSettings(gatewayAddress, settings);
                settings.Set<WormHoleRoutingSettings>(routingSettings);
            }
            return routingSettings;
        }
    }
}