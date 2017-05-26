namespace NServiceBus.Transports.Http
{
    using System;

    class AddressParser
    {
        int defaultPort;
        string defaultHost;

        public AddressParser(int defaultPort, string defaultHost)
        {
            this.defaultPort = defaultPort;
            this.defaultHost = defaultHost;
        }

        public string ParseAddress(string address)
        {
            return address.StartsWith("http://") ?
                address.TrimEnd('/')
                : $"http://{defaultHost}:{defaultPort}/{address}";
        }

        public string GenerateAddress(LogicalAddress logicalAddress)
        {
            string host;
            string port;
            int portNumber;
            if (!logicalAddress.EndpointInstance.Properties.TryGetValue("Host", out host))
            {
                host = defaultHost;
            }
            if (!logicalAddress.EndpointInstance.Properties.TryGetValue("Port", out port))
            {
                port = defaultPort.ToString();
            }
            else if (!int.TryParse(port, out portNumber) || portNumber < 1 || portNumber > 65535)
            {
                throw new Exception("Port number has to be an integer between 1 and 65535");
            }

            var discriminatorPart = logicalAddress.EndpointInstance.Discriminator != null
                ? "-" + logicalAddress.EndpointInstance.Discriminator
                : "";

            var qualifierPart = logicalAddress.Qualifier != null
                ? "/" + logicalAddress.Qualifier
                : "";

            return $"http://{host}:{port}/{logicalAddress.EndpointInstance.Endpoint}{discriminatorPart}{qualifierPart}";
        }
    }
}