namespace NServiceBus.Transports.Http
{
    using Routing;
    using Settings;
    using Transport;
    public class HttpTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            return new HttpTransportInfrastructure(settings, connectionString);
        }

        public override bool RequiresConnectionString => false;

        public override string ExampleConnectionStringForErrorMessage => "";
    }
}
