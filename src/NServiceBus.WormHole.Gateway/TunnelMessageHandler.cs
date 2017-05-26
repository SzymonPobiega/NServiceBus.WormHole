namespace NServiceBus.WormHole.Gateway
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Raw;
    using Routing;
    using Transport;

    class TunnelMessageHandler
    {
        MessageRouter router;

        public TunnelMessageHandler(MessageRouter router)
        {
            this.router = router;
        }

        public Task Handle(MessageContext context, IRawEndpoint siteDispatcher)
        {
            string enclosedTypes;
            string destinationAddress;
            string replyTo;
            if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out enclosedTypes))
            {
                throw new GatewayException("The message does not contain the NServiceBus.EnclosedMessageTypes header");
            }

            //If there is a reply-to header, substitute it with the gateway queue
            if (context.Headers.TryGetValue(Headers.ReplyToAddress, out replyTo))
            {
                context.Headers["NServiceBus.WormHole.ReplyToAddress"] = replyTo;
                context.Headers[Headers.ReplyToAddress] = siteDispatcher.TransportAddress;
            }

            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);

            if (context.Headers.TryGetValue("NServiceBus.WormHole.Destination", out destinationAddress)) //In case the source site specified the destination
            {
                var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destinationAddress));
                return siteDispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag());
            }

            //Route according to local information
            var qualifiedNames = enclosedTypes.Split(new[]
            {
                ';'
            }, StringSplitOptions.RemoveEmptyEntries);
            var mainType = MessageType.Parse(qualifiedNames.First());

            var routes = router.Route(mainType, i => siteDispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)));

            if (routes.Length > 0)
            {
                var ops = routes.Select(r => new TransportOperation(outgoingMessage, new UnicastAddressTag(r))).ToArray();
                return siteDispatcher.Dispatch(new TransportOperations(ops), new TransportTransaction(), new ContextBag());
            }
            throw new GatewayException($"No route specified for message type(s) {enclosedTypes}");
        }
    }
}