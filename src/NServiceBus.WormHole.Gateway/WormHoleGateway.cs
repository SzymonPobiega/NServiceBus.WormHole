namespace NServiceBus.WormHole.Gateway
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Logging;
    using Raw;
    using Routing;
    using Transport;

    class WormHoleGateway<TLocalTransport, TWormHoleTransport> : IWormHoleGateway, IStartableWormHoleGateway
        where TLocalTransport : TransportDefinition, new()
        where TWormHoleTransport : TransportDefinition, new()
    {
        internal WormHoleGateway(string name, TunnelMessageHandler tunnelMessageHandler, SiteMessageHandler siteMessageHandler, string poisonMessageQueue,
            Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> localEndpointCustomization,
            Action<RawEndpointConfiguration, TransportExtensions<TWormHoleTransport>> wormholeEndpointCustomization)
        {
            this.name = name;
            this.poisonMessageQueue = poisonMessageQueue;
            this.localEndpointCustomization = localEndpointCustomization;
            this.wormholeEndpointCustomization = wormholeEndpointCustomization;
            this.tunnelMessageHandler = tunnelMessageHandler;
            this.siteMessageHandler = siteMessageHandler;
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

        public async Task Stop()
        {
            //Ensure no more messages are in-flight
            var wormHoleStoppable = await wormHoleInstance.StopReceiving().ConfigureAwait(false);
            var outsideStoppable = await localInstance.StopReceiving().ConfigureAwait(false);

            //Stop the transport
            await wormHoleStoppable.Stop().ConfigureAwait(false);
            await outsideStoppable.Stop().ConfigureAwait(false);
        }

        static Task OnWormHoleMessage(MessageContext context, IRawEndpoint outsideEndpoint)
        {
            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var op = new TransportOperation(outgoingMessage, new UnicastAddressTag(outsideEndpoint.TransportAddress));
            return outsideEndpoint.Dispatch(new TransportOperations(op), new TransportTransaction(), new ContextBag());
        }

        Task OnOutsideMessage(MessageContext context, IRawEndpoint wormHoleEndpoint, IRawEndpoint outsideEndpoint)
        {
            string sourceSite;
            if (context.Headers.TryGetValue("NServiceBus.WormHole.SourceSite", out sourceSite))
            {
                return tunnelMessageHandler.Handle(context, outsideEndpoint);
            }
            return siteMessageHandler.Handle(context, wormHoleEndpoint);
        }

        ILog log = LogManager.GetLogger(typeof(WormHoleGateway<,>));
        string name;
        TunnelMessageHandler tunnelMessageHandler;
        SiteMessageHandler siteMessageHandler;
        string poisonMessageQueue;
        Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> localEndpointCustomization;
        Action<RawEndpointConfiguration, TransportExtensions<TWormHoleTransport>> wormholeEndpointCustomization;
        IReceivingRawEndpoint localInstance;
        IReceivingRawEndpoint wormHoleInstance;

        class OutsideErrorHandlingPolicy : IErrorHandlingPolicy
        {
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

            string errorQueue;
            int immediateRetryCount;
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