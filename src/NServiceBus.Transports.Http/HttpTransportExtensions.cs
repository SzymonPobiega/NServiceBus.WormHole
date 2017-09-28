namespace NServiceBus.Transports.Http
{
    using System;
    using System.Net.Http;
    using Configuration.AdvancedExtensibility;

    /// <summary>
    /// Configures HTTP transport
    /// </summary>
    public static class HttpTransportExtensions
    {
        /// <summary>
        /// Instructs the transport to use provided instance of HttpClient
        /// </summary>
        /// <returns></returns>
        public static TransportExtensions<HttpTransport> UseHttpClient(this TransportExtensions<HttpTransport> config, HttpClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            config.GetSettings().Set<HttpClientHolder>(new HttpClientHolder(client));
            return config;
        }
    }
}