namespace NServiceBus.AcceptanceTests
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Logging;
    using NUnit.Framework;
    using Raw;
    using Transports.Http;

    public abstract partial class NServiceBusAcceptanceTest
    {
        IReceivingRawEndpoint errorListener;
        static HttpClient httpClient = new HttpClient();

        [SetUp]
        public void StartErrorQueueListener()
        {
            var config = RawEndpointConfiguration.Create("error", (context, messages) =>
            {
                return Task.FromResult(0);
            }, "poison");
            config.UseTransport<HttpTransport>().UseHttpClient(httpClient);

            LogManager.Use<DefaultFactory>();
            errorListener = RawEndpoint.Start(config).GetAwaiter().GetResult();
        }

        [TearDown]
        public void StopErrorQueueListener()
        {
            LogManager.Use<DefaultFactory>();
            errorListener.StopReceiving().GetAwaiter().GetResult().Stop().GetAwaiter().GetResult();
        }
    }
}