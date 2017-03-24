using System.Text;

namespace NServiceBus.Transports.Http
{
    using Settings;
    using Transport;
    public class HttpTransport : TransportDefinition
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            return new HttpTransportInfrastructure(settings, connectionString);
        }

        public override bool RequiresConnectionString => false;

        public override string ExampleConnectionStringForErrorMessage => "";
    }
}
