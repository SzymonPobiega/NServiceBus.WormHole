namespace NServiceBus.Wormhole
{
    using System;
    using Configuration.AdvanceExtensibility;
    using Features;

    public static class WormholeRoutingExtensions
    {
        /// <summary>
        /// Enables the Worm Hole gateway.
        /// </summary>
        /// <param name="config">Endpoint config.</param>
        /// <param name="gatewayAddress">Worm Hole gateway address.</param>
        public static WormholeRoutingSettings UseWormholeGateway(this EndpointConfiguration config, string gatewayAddress)
        {
            if (gatewayAddress == null)
            {
                throw new ArgumentNullException(nameof(gatewayAddress));
            }
            var settings = config.GetSettings();
            settings.EnableFeatureByDefault<WormholeRoutingFeature>();
            WormholeRoutingSettings routingSettings;
            if (!settings.TryGet(out routingSettings))
            {
                routingSettings = new WormholeRoutingSettings(gatewayAddress, settings);
                settings.Set<WormholeRoutingSettings>(routingSettings);
            }
            return routingSettings;
        }
    }
}