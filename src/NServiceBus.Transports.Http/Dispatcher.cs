namespace NServiceBus.Transports.Http
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Extensibility;
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
            var requestMessages = outgoingMessages.UnicastTransportOperations
                .Select(CreateRequestMessage);

            foreach (var message in requestMessages)
            {
                await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);
            }
        }

        HttpRequestMessage CreateRequestMessage(UnicastTransportOperation op)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, op.Destination.TrimEnd('/') + "/" + op.Message.MessageId);
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