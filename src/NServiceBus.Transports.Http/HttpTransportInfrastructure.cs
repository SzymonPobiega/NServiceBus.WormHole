namespace NServiceBus.Transports.Http
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Routing;
    using Settings;
    using Transport;

    class HttpTransportInfrastructure : TransportInfrastructure
    {
        SettingsHolder settings;
        string connectionString;

        public HttpTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;
            this.connectionString = connectionString;
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                () => new MessagePump(connectionString), 
                () => new QueueCreator(connectionString), 
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
                () => new Dispatcher(), 
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotImplementedException();
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            throw new NotImplementedException();
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<Type> DeliveryConstraints => new Type[0];
        public override TransportTransactionMode TransactionMode => TransportTransactionMode.None;
        public override OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);
    }
}