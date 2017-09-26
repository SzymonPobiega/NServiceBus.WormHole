namespace NServiceBus.Transports.Http
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Routing;
    using Settings;
    using Support;
    using Transport;

    class HttpTransportInfrastructure : TransportInfrastructure
    {
        SettingsHolder settings;
        AddressParser addressParser;

        public HttpTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;
            addressParser = new AddressParser(7777, RuntimeEnvironment.MachineName);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                () => new MessagePump(), 
                () => new QueueCreator(addressParser), 
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            if (!settings.TryGet("NServiceBus.Transports.Http.HttpClient", out HttpClient client))
            {
                client = new HttpClient();
            }

            return new TransportSendInfrastructure(
                () => new Dispatcher(addressParser, client), 
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
            return addressParser.GenerateAddress(logicalAddress);
        }

        public override IEnumerable<Type> DeliveryConstraints => new Type[0];
        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;
        public override OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);
    }
}