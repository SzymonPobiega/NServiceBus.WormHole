using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace NServiceBus.WormHole.Gateway
{
    public class WormHoleGateway<TLocalTransport, TWormHoleTransport> : IWormHoleGateway
        where TLocalTransport : TransportDefinition, new()
        where TWormHoleTransport : TransportDefinition, new()
    {
        const string CorrelationIdHeader = "NServiceBus.WormHole|";
        string name;
        string thisSite;
        MessageRouter router;
        Dictionary<string, string> sites;
        string poisonMessageQueue;
        Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> localEndpointCustomization;
        Action<RawEndpointConfiguration, TransportExtensions<TWormHoleTransport>> wormholeEndpointCustomization;
        IRawEndpointInstance localInstance;
        IRawEndpointInstance wormHoleInstance;
        ISendRawMessages localDispatcher;
        ISendRawMessages wormHoleDispatcher;

        internal WormHoleGateway(string name, string site, MessageRouter router, Dictionary<string, string> sites, string poisonMessageQueue,
            Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> localEndpointCustomization,
            Action<RawEndpointConfiguration, TransportExtensions<TWormHoleTransport>> wormholeEndpointCustomization)
        {
            this.name = name;
            this.thisSite = site;
            this.sites = sites;
            this.poisonMessageQueue = poisonMessageQueue;
            this.localEndpointCustomization = localEndpointCustomization;
            this.wormholeEndpointCustomization = wormholeEndpointCustomization;
            this.router = router;
        }

        public async Task<IWormHoleGateway> Start()
        {
            var outsideConfig = RawEndpointConfiguration.Create(name, OnOutsideMessage, poisonMessageQueue);
            var wormHoleConfig = RawEndpointConfiguration.Create(name, OnWormHoleMessage, poisonMessageQueue);

            var outsideTransport = outsideConfig.UseTransport<TLocalTransport>();
            localEndpointCustomization(outsideConfig, outsideTransport);

            var wormHoleTransport = wormHoleConfig.UseTransport<TWormHoleTransport>();
            wormholeEndpointCustomization(wormHoleConfig, wormHoleTransport);

            var outsideStartable = await RawEndpoint.Create(outsideConfig).ConfigureAwait(false);
            var wormHoleStartable = await RawEndpoint.Create(wormHoleConfig).ConfigureAwait(false);

            //Store the dispatchers in fields
            localDispatcher = outsideStartable;
            wormHoleDispatcher = wormHoleStartable;

            //Start receiving
            localInstance = await outsideStartable.Start().ConfigureAwait(false);
            wormHoleInstance = await wormHoleStartable.Start().ConfigureAwait(false);

            return this;
        }

        Task OnWormHoleMessage(MessageContext context, IDispatchMessages dispatcher)
        {
            string enclosedTypes;
            string sourceSite;
            string correlationId;
            string destinationAddress;
            string replyTo;
            if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out enclosedTypes))
            {
                MoveToPoisonMessageQueue(context, "The message does not contain the NServiceBus.EnclosedMessageTypes header");
            }
            if (!context.Headers.TryGetValue("NServiceBus.WormHole.SourceSite", out sourceSite))
            {
                MoveToPoisonMessageQueue(context, "The message does not contain the NServiceBus.WormHole.SourceSite header.");
            }
            if (!context.Headers.TryGetValue(Headers.CorrelationId, out correlationId))
            {
                MoveToPoisonMessageQueue(context, "The message does not contain the NServiceBus.CorrelationId header.");
            }

            var props = new Dictionary<string, string> { ["Site"] = sourceSite };

            //If there is a reply-to header, substitute it with the gateway queue
            if (context.Headers.TryGetValue(Headers.ReplyToAddress, out replyTo))
            {
                props["ReplyTo"] = replyTo;
                context.Headers[Headers.ReplyToAddress] = name; //TODO: Use proper address
            }

            context.Headers[Headers.CorrelationId] = $"{CorrelationIdHeader}{props.EncodeTLV()}";
            context.Headers.Remove("NServiceBus.WormHole.SourceSite");

            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);

            if (context.Headers.TryGetValue("NServiceBus.WormHole.Destination", out destinationAddress)) //In case the source site specified the destination
            {
                var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destinationAddress));
                return localDispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag());
            }

            //Route according to local information
            var qualifiedNames = enclosedTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var mainType = MessageType.Parse(qualifiedNames.First());

            var routes = router.Route(mainType, i => localDispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)));

            
            if (routes.Length > 0)
            {
                var ops = routes.Select(r => new TransportOperation(outgoingMessage, new UnicastAddressTag(r))).ToArray();
                return localDispatcher.Dispatch(new TransportOperations(ops), new TransportTransaction(), new ContextBag());
            }
            return MoveToPoisonMessageQueue(context, $"No route specified for message type(s) {enclosedTypes}");
        }

        Task OnOutsideMessage(MessageContext context, IDispatchMessages dispatcher)
        {
            string destinationSites;
            if (!context.Headers.TryGetValue("NServiceBus.WormHole.DestinationSites", out destinationSites))
            {
                //This can be a reply for a worm hole message. Check the correlation ID
                string correlation;
                if (!context.Headers.TryGetValue(Headers.CorrelationId, out correlation))
                {
                    return MoveToPoisonMessageQueue(context, "The message does not contain site information nor correlation ID.");
                }
                if (!correlation.StartsWith(CorrelationIdHeader))
                {
                    return MoveToPoisonMessageQueue(context, $"The correlation ID does not begin with an expected header {CorrelationIdHeader}.");
                }
                var trimmed = correlation.Substring(CorrelationIdHeader.Length);
                var props = trimmed.DecodeTLV();
                if (!props.TryGetValue("Site", out destinationSites))
                {
                    return MoveToPoisonMessageQueue(context, "The correlation ID does not contain site information.");
                }
                string originalReplyTo;
                if (!props.TryGetValue("ReplyTo", out originalReplyTo))
                {
                    return MoveToPoisonMessageQueue(context, "The correlation ID does not contain reply address information.");
                }
                context.Headers["NServiceBus.WormHole.Destination"] = originalReplyTo;
            }

            var siteList = destinationSites.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);
            var siteAddresses = siteList.Select(ResolveAddress).ToArray();
            if (siteAddresses.Contains(null))
            {
                return MoveToPoisonMessageQueue(context, $"Cannot resolve addresses of one or more sites: {sites}.");
            }

            context.Headers.Remove("NServiceBus.WormHole.DestinationSites"); //Destination site not relevant
            context.Headers["NServiceBus.WormHole.SourceSite"] = thisSite; //Will be used when reply-to

            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var ops = siteAddresses.Select(a => new TransportOperation(outgoingMessage, new UnicastAddressTag(a))).ToArray();
            return wormHoleDispatcher.Dispatch(new TransportOperations(ops), new TransportTransaction(), new ContextBag());
        }

        string ResolveAddress(string siteName)
        {
            string siteAddress;
            sites.TryGetValue(siteName, out siteAddress);
            return siteAddress;
        }

        Task MoveToPoisonMessageQueue(MessageContext context, string message = null)
        {
            //TODO
            Console.WriteLine(message ?? "Boom!");
            return Task.CompletedTask;
        }

        public async Task Stop()
        {
            //Ensure no more messages are in-flight
            var wormHoleStoppable = await wormHoleInstance.StopReceiving().ConfigureAwait(false);
            var outsideStoppable = await localInstance.StopReceiving().ConfigureAwait(false);

            //Stop the transport
            await wormHoleStoppable.Stop().ConfigureAwait(false);
            await outsideStoppable.Stop().ConfigureAwait(false);
        }
    }
}
