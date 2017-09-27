namespace NServiceBus.Transports.Http
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    class Dispatcher : IDispatchMessages
    {
        AddressParser addressParser;
        HttpClient client;

        public Dispatcher(AddressParser addressParser, HttpClient httpClient)
        {
            this.addressParser = addressParser;
            client = httpClient;
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
                using (var httpMessage = CreateRequestMessage(operation))
                {
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
        }

        HttpRequestMessage CreateRequestMessage(UnicastTransportOperation op)
        {
            var prefix = addressParser.ParseAddress(op.Destination);
            var request = new HttpRequestMessage(HttpMethod.Post, prefix + "/" + op.Message.MessageId);
            request.Content = new ByteArrayContent(op.Message.Body);
            foreach (var header in op.Message.Headers)
            {
                request.Headers.Add("X-NSB-" + WebUtility.UrlEncode(header.Key), WebUtility.UrlEncode(header.Value));
            }
            return request;
        }
    }
}