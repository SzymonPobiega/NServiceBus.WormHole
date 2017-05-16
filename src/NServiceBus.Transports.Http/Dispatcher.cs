namespace NServiceBus.Transports.Http
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Extensibility;
    using Support;
    using Transport;

    class Dispatcher : IDispatchMessages, IDisposable
    {
        HttpClient client;

        public Dispatcher()
        {
            client = new HttpClient();
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            foreach (var message in outgoingMessages.UnicastTransportOperations)
            {
                await SendWithRetries(message).ConfigureAwait(false);
            }
        }

        async Task SendWithRetries(UnicastTransportOperation operation)
        {
            var retries = 0;
            while (true)
            {
                var httpMessage = CreateRequestMessage(operation);
                if (retries > 0)
                {
                    httpMessage.Headers.Add("X-NSBHttp-ImmediateFailures", retries.ToString(CultureInfo.InvariantCulture));
                }
                var response = await client.SendAsync(httpMessage, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable) //Immediare retry
                {
                    retries++;
                }
                else
                {
                    throw new Exception($"Unexpected status code {response.StatusCode} when sending to {httpMessage.RequestUri}.");
                }
            }
        }

        static HttpRequestMessage CreateRequestMessage(UnicastTransportOperation op)
        {
            string prefix;
            if (!op.Destination.StartsWith("http://"))
            {
                prefix = $"http://{RuntimeEnvironment.MachineName}:7777/{op.Destination}";
            }
            else
            {
                prefix = op.Destination.TrimEnd('/');
            }
            var request = new HttpRequestMessage(HttpMethod.Post, prefix + "/" + op.Message.MessageId);
            request.Content = new ByteArrayContent(op.Message.Body);
            foreach (var header in op.Message.Headers)
            {
                request.Headers.Add("X-NSB-" + WebUtility.UrlEncode(header.Key), WebUtility.UrlEncode(header.Value));
            }
            return request;
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}