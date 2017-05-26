namespace NServiceBus.Transports.Http
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Routing;
    using Settings;
    using Support;
    using Transport;

    class HttpTransportInfrastructure : TransportInfrastructure
    {
        AddressParser addressParser;

        public HttpTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            addressParser = new AddressParser(7777, RuntimeEnvironment.MachineName);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                () => new MessagePump(), 
                () => new QueueCreator(), 
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
            return instance.SetProperty("Host", RuntimeEnvironment.MachineName);
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            string host;
            if (!logicalAddress.EndpointInstance.Properties.TryGetValue("Host", out host))
            {
                host = RuntimeEnvironment.MachineName;
            }

            var discriminatorPart = logicalAddress.EndpointInstance.Discriminator != null
                ? "-" + logicalAddress.EndpointInstance.Discriminator
                : "";

            var qualifierPart = logicalAddress.Qualifier != null
                ? "/" + logicalAddress.Qualifier
                : "";

            return $"http://{host}:7777/{logicalAddress.EndpointInstance.Endpoint}{discriminatorPart}{qualifierPart}";
        }

        public override IEnumerable<Type> DeliveryConstraints => new Type[0];
        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;
        public override OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);
    }
}