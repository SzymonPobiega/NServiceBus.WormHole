using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace NServiceBus.WormHole.Gateway
{
    using Logging;

    class WormHoleGateway<TLocalTransport, TWormHoleTransport> : IWormHoleGateway, IStartableWormHoleGateway
        where TLocalTransport : TransportDefinition, new()
        where TWormHoleTransport : TransportDefinition, new()
    {
        ILog log = LogManager.GetLogger(typeof(WormHoleGateway<,>));

        const string CorrelationIdHeader = "NServiceBus.WormHole|";
        string name;
        string thisSite;
        MessageRouter router;
        Dictionary<string, string> sites;
        string poisonMessageQueue;
        Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> localEndpointCustomization;
        Action<RawEndpointConfiguration, TransportExtensions<TWormHoleTransport>> wormholeEndpointCustomization;
        IReceivingRawEndpoint localInstance;
        IReceivingRawEndpoint wormHoleInstance;

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
            IStartableRawEndpoint outsideStartable = null;
            IStartableRawEndpoint wormHoleStartable = null;

            var outsideConfig = RawEndpointConfiguration.Create(name, (c, _) => OnOutsideMessage(c, wormHoleStartable, outsideStartable), poisonMessageQueue);
            var wormHoleConfig = RawEndpointConfiguration.Create(name, (c, _) => OnWormHoleMessage(c, outsideStartable), poisonMessageQueue);

            outsideConfig.AutoCreateQueue();
            wormHoleConfig.AutoCreateQueue();

            outsideConfig.CustomErrorHandlingPolicy(new OutsideErrorHandlingPolicy(poisonMessageQueue, 5));
            wormHoleConfig.CustomErrorHandlingPolicy(new GatewayErrorHandlingPolicy());

            var outsideTransport = outsideConfig.UseTransport<TLocalTransport>();
            localEndpointCustomization(outsideConfig, outsideTransport);

            var wormHoleTransport = wormHoleConfig.UseTransport<TWormHoleTransport>();
            wormholeEndpointCustomization(wormHoleConfig, wormHoleTransport);

            outsideStartable = await RawEndpoint.Create(outsideConfig).ConfigureAwait(false);
            wormHoleStartable = await RawEndpoint.Create(wormHoleConfig).ConfigureAwait(false);

            //Start receiving
            localInstance = await outsideStartable.Start().ConfigureAwait(false);
            wormHoleInstance = await wormHoleStartable.Start().ConfigureAwait(false);

            return this;
        }

        static Task OnWormHoleMessage(MessageContext context, IRawEndpoint outsideEndpoint)
        {
            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var op = new TransportOperation(outgoingMessage, new UnicastAddressTag(outsideEndpoint.TransportAddress));
            return outsideEndpoint.Dispatch(new TransportOperations(op), new TransportTransaction(), new ContextBag());
        }

        Task OnWormHoleMessage(string sourceSite, MessageContext context, IRawEndpoint outsideEndpoint)
        {
            string enclosedTypes;
            string destinationAddress;
            string replyTo;
            if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out enclosedTypes))
            {
                MoveToPoisonMessageQueue("The message does not contain the NServiceBus.EnclosedMessageTypes header");
            }
            
            //If there is a reply-to header, substitute it with the gateway queue
            if (context.Headers.TryGetValue(Headers.ReplyToAddress, out replyTo))
            {
                context.Headers["NServiceBus.WormHole.ReplyToAddress"] = replyTo;
                context.Headers[Headers.ReplyToAddress] = outsideEndpoint.TransportAddress;
            }
            
            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);

            if (context.Headers.TryGetValue("NServiceBus.WormHole.Destination", out destinationAddress)) //In case the source site specified the destination
            {
                var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destinationAddress));
                return outsideEndpoint.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag());
            }

            //Route according to local information
            var qualifiedNames = enclosedTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var mainType = MessageType.Parse(qualifiedNames.First());

            var routes = router.Route(mainType, i => outsideEndpoint.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)));

            if (routes.Length > 0)
            {
                var ops = routes.Select(r => new TransportOperation(outgoingMessage, new UnicastAddressTag(r))).ToArray();
                return outsideEndpoint.Dispatch(new TransportOperations(ops), new TransportTransaction(), new ContextBag());
            }
            return MoveToPoisonMessageQueue($"No route specified for message type(s) {enclosedTypes}");
        }

        Task OnOutsideMessage(MessageContext context, IRawEndpoint wormHoleEndpoint, IRawEndpoint outsideEndpoint)
        {
            string sourceSite;
            if (context.Headers.TryGetValue("NServiceBus.WormHole.SourceSite", out sourceSite))
            {
                return OnWormHoleMessage(sourceSite, context, outsideEndpoint);
            }

            string destinationSites;
            if (!context.Headers.TryGetValue("NServiceBus.WormHole.DestinationSites", out destinationSites))
            {
                return MoveToPoisonMessageQueue("Message has no 'NServiceBus.WormHole.DestinationSites' header.");
            }

            var siteList = destinationSites.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var siteAddresses = siteList.Select(ResolveAddress).ToArray();
            if (siteAddresses.Contains(null))
            {
                return MoveToPoisonMessageQueue($"Cannot resolve addresses of one or more sites: {string.Join(",", siteList)}.");
            }

            context.Headers.Remove("NServiceBus.WormHole.DestinationSites"); //Destination site not relevant
            context.Headers["NServiceBus.WormHole.SourceSite"] = thisSite; //Will be used when reply-to

            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var ops = siteAddresses.Select(a => new TransportOperation(outgoingMessage, new UnicastAddressTag(a))).ToArray();
            return wormHoleEndpoint.Dispatch(new TransportOperations(ops), new TransportTransaction(), new ContextBag());
        }

        string ResolveAddress(string siteName)
        {
            string siteAddress;
            sites.TryGetValue(siteName, out siteAddress);
            return siteAddress;
        }

        static Task MoveToPoisonMessageQueue(string message)
        {
            throw new GatewayException(message);
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

        class OutsideErrorHandlingPolicy : IErrorHandlingPolicy
        {
            string errorQueue;
            int immediateRetryCount;

            public OutsideErrorHandlingPolicy(string errorQueue, int immediateRetryCount)
            {
                this.errorQueue = errorQueue;
                this.immediateRetryCount = immediateRetryCount;
            }

            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                if (handlingContext.Error.Exception is GatewayException)
                {
                    return handlingContext.MoveToErrorQueue(errorQueue);
                }
                if (handlingContext.Error.ImmediateProcessingFailures < immediateRetryCount)
                {
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                }
                return handlingContext.MoveToErrorQueue(errorQueue);
            }
        }

        class GatewayErrorHandlingPolicy : IErrorHandlingPolicy
        {
            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                return Task.FromResult(ErrorHandleResult.RetryRequired);
            }
        }
    }
}
