namespace NServiceBus.Wormhole.Gateway
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Logging;
    using Raw;
    using Routing;
    using Transport;

    class WormholeGateway<TLocalTransport, TWormholeTransport> : IWormholeGateway, IStartableWormholeGateway
        where TLocalTransport : TransportDefinition, new()
        where TWormholeTransport : TransportDefinition, new()
    {
        internal WormholeGateway(string name, TunnelMessageHandler tunnelMessageHandler, SiteMessageHandler siteMessageHandler, string poisonMessageQueue,
            Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> localEndpointCustomization,
            Action<RawEndpointConfiguration, TransportExtensions<TWormholeTransport>> WormholeEndpointCustomization)
        {
            this.name = name;
            this.poisonMessageQueue = poisonMessageQueue;
            this.localEndpointCustomization = localEndpointCustomization;
            this.WormholeEndpointCustomization = WormholeEndpointCustomization;
            this.tunnelMessageHandler = tunnelMessageHandler;
            this.siteMessageHandler = siteMessageHandler;
        }

        public async Task<IWormholeGateway> Start()
        {
            IStartableRawEndpoint outsideStartable = null;
            IStartableRawEndpoint WormholeStartable = null;

            var outsideConfig = RawEndpointConfiguration.Create(name, (c, _) => OnOutsideMessage(c, WormholeStartable, outsideStartable), poisonMessageQueue);
            var WormholeConfig = RawEndpointConfiguration.Create(name, (c, _) => OnWormholeMessage(c, outsideStartable), poisonMessageQueue);

            outsideConfig.AutoCreateQueue();
            WormholeConfig.AutoCreateQueue();

            outsideConfig.CustomErrorHandlingPolicy(new OutsideErrorHandlingPolicy(poisonMessageQueue, 5));
            WormholeConfig.CustomErrorHandlingPolicy(new GatewayErrorHandlingPolicy());

            var outsideTransport = outsideConfig.UseTransport<TLocalTransport>();
            localEndpointCustomization(outsideConfig, outsideTransport);

            var WormholeTransport = WormholeConfig.UseTransport<TWormholeTransport>();
            WormholeEndpointCustomization(WormholeConfig, WormholeTransport);

            outsideStartable = await RawEndpoint.Create(outsideConfig).ConfigureAwait(false);
            WormholeStartable = await RawEndpoint.Create(WormholeConfig).ConfigureAwait(false);

            //Start receiving
            localInstance = await outsideStartable.Start().ConfigureAwait(false);
            WormholeInstance = await WormholeStartable.Start().ConfigureAwait(false);

            return this;
        }

        public async Task Stop()
        {
            //Ensure no more messages are in-flight
            var WormholeStoppable = await WormholeInstance.StopReceiving().ConfigureAwait(false);
            var outsideStoppable = await localInstance.StopReceiving().ConfigureAwait(false);

            //Stop the transport
            await WormholeStoppable.Stop().ConfigureAwait(false);
            await outsideStoppable.Stop().ConfigureAwait(false);
        }

        static Task OnWormholeMessage(MessageContext context, IRawEndpoint outsideEndpoint)
        {
            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var op = new TransportOperation(outgoingMessage, new UnicastAddressTag(outsideEndpoint.TransportAddress));
            return outsideEndpoint.Dispatch(new TransportOperations(op), new TransportTransaction(), new ContextBag());
        }

        Task OnOutsideMessage(MessageContext context, IRawEndpoint WormholeEndpoint, IRawEndpoint outsideEndpoint)
        {
            if (context.Headers.ContainsKey("NServiceBus.Wormhole.SourceSite"))
            {
                return tunnelMessageHandler.Handle(context, outsideEndpoint);
            }
            return siteMessageHandler.Handle(context, WormholeEndpoint);
        }

        string name;
        TunnelMessageHandler tunnelMessageHandler;
        SiteMessageHandler siteMessageHandler;
        string poisonMessageQueue;
        Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> localEndpointCustomization;
        Action<RawEndpointConfiguration, TransportExtensions<TWormholeTransport>> WormholeEndpointCustomization;
        IReceivingRawEndpoint localInstance;
        IReceivingRawEndpoint WormholeInstance;

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