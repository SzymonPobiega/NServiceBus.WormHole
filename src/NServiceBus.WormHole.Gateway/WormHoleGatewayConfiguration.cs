using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Settings;
using NServiceBus.Transport;

namespace NServiceBus.WormHole.Gateway
{
    public class WormHoleGatewayConfiguration<TLocalTransport, TWormHoleTransport>
        where TLocalTransport : TransportDefinition, new()
        where TWormHoleTransport : TransportDefinition, new()
    {
        string queueName;
        string site;

        List<RoutingTableEntry> routes = new List<RoutingTableEntry>();
        Dictionary<string, string> sites = new Dictionary<string, string>();

        Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> localTransportCustomization = (c, t) => { };
        Action<RawEndpointConfiguration, TransportExtensions<TWormHoleTransport>> wormHoleTransportCustomization = (c, t) => { };

        /// <summary>
        /// Routing table.
        /// </summary>
        public RoutingTable RoutingTable { get; } = new RoutingTable();

        /// <summary>
        /// Endpoint instances.
        /// </summary>
        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        /// <summary>
        /// Distribution policy.
        /// </summary>
        public DistributionPolicy DistributionPolicy { get; } = new DistributionPolicy();

        public WormHoleGatewayConfiguration(string queueName, string site)
        {
            this.queueName = queueName;
            this.site = site;
        }

        /// <summary>
        /// Registers a callback for customizing the local transport.
        /// </summary>
        public void CustomizeLocalTransport(Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> customization)
        {
            if (customization == null)
            {
                throw new ArgumentNullException(nameof(customization));
            }
            localTransportCustomization = customization;
        }

        /// <summary>
        /// Registers a callback for customizing the worm hole transport.
        /// </summary>
        public void CustomizeWormHoleTransport(Action<RawEndpointConfiguration, TransportExtensions<TWormHoleTransport>> customization)
        {
            if (customization == null)
            {
                throw new ArgumentNullException(nameof(customization));
            }
            wormHoleTransportCustomization = customization;
        }

        /// <summary>
        /// Configures destination transport address for a given site.
        /// </summary>
        /// <param name="site">Site</param>
        /// <param name="destinationAddress">Destination address.</param>
        public void ConfigureSite(string site, string destinationAddress)
        {
            if (site == null)
            {
                throw new ArgumentNullException(nameof(site));
            }
            if (destinationAddress == null)
            {
                throw new ArgumentNullException(nameof(destinationAddress));
            }
            sites[site] = destinationAddress;
        }

        /// <summary>
        /// Instructs the gateway to forward messages of a given type to a given endpoint.
        /// </summary>
        /// <param name="messageType">The message which should be forwarded.</param>
        /// <param name="destination">The destination endpoint.</param>
        public void ForwardToEndpoint(MessageType messageType, string destination)
        {
            ThrowOnAddress(destination);
            routes.Add(new RoutingTableEntry(messageType, UnicastRoute.CreateFromEndpointName(destination)));
        }

        /// <summary>
        /// Instructs the gateway to forward messages from a given assembly to a given endpoint.
        /// </summary>
        /// <param name="assembly">The assembly whose messages should be forwarded.</param>
        /// <param name="destination">Destination endpoint.</param>
        public void ForwardToEndpoint(string assembly, string destination)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            ThrowOnAddress(destination);

            routes.Add(new RoutingTableEntry(new MessageTypeRange(assembly, null), UnicastRoute.CreateFromEndpointName(destination)));
        }

        /// <summary>
        /// Instructs the gateway to forward messages from a given assembly and namespace to a given endpoint.
        /// </summary>
        /// <param name="assembly">The assembly whose messages should be routed.</param>
        /// <param name="namespace">The namespace of the messages which should be forwarded. The given value must exactly match the target namespace.</param>
        /// <param name="destination">Destination endpoint.</param>
        public void ForwardToEndpoint(string assembly, string @namespace, string destination)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }
            if (@namespace == null)
            {
                throw new ArgumentNullException(nameof(@namespace));
            }
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            ThrowOnAddress(destination);

            routes.Add(new RoutingTableEntry(new MessageTypeRange(assembly, @namespace), UnicastRoute.CreateFromEndpointName(destination)));
        }

        public WormHoleGateway<TLocalTransport, TWormHoleTransport> Build()
        {
            RoutingTable.AddOrReplaceRoutes("StaticConfiguration", routes);

            var router = new MessageRouter(RoutingTable, EndpointInstances, DistributionPolicy);

            return new WormHoleGateway<TLocalTransport, TWormHoleTransport>(queueName, site, router, sites, "poison",
                localTransportCustomization, wormHoleTransportCustomization);
        }

        public Task<IWormHoleGateway> Start()
        {
            var gatway = Build();
            return gatway.Start();
        }

        static void ThrowOnAddress(string destination)
        {
            if (destination.Contains("@"))
            {
                throw new ArgumentException($"A logical endpoint name should not contain '@', but received '{destination}'. To specify an endpoint's address, use the instance mapping file for the MSMQ transport, or refer to the routing documentation.");
            }
        }
    }
}