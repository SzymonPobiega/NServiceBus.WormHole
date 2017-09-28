namespace NServiceBus.Transports.Http
{
    using System.Net.Http;

    class HttpClientHolder
    {
        public HttpClientHolder(HttpClient httpClient)
        {
            Client = httpClient;
        }

        public HttpClient Client { get; }
    }
}