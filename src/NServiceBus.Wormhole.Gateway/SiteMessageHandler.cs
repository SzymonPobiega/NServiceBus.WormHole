namespace NServiceBus.Wormhole.Gateway
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Routing;
    using Transport;

    class SiteMessageHandler
    {
        public SiteMessageHandler(string thisSite, Dictionary<string, string> sites)
        {
            this.sites = sites;
            this.thisSite = thisSite;
        }

        public Task Handle(MessageContext context, IDispatchMessages tunnelDispatcher)
        {
            string destinationSites;
            if (!context.Headers.TryGetValue("NServiceBus.Wormhole.DestinationSites", out destinationSites))
            {
                throw new GatewayException("Message has no 'NServiceBus.Wormhole.DestinationSites' header.");
            }

            var siteList = destinationSites.Split(new[]
            {
                ';'
            }, StringSplitOptions.RemoveEmptyEntries);
            var siteAddresses = siteList.Select(ResolveAddress).ToArray();
            if (siteAddresses.Contains(null))
            {
                throw new GatewayException($"Cannot resolve addresses of one or more sites: {string.Join(",", siteList)}.");
            }

            context.Headers.Remove("NServiceBus.Wormhole.DestinationSites"); //Destination site not relevant
            context.Headers["NServiceBus.Wormhole.SourceSite"] = thisSite; //Will be used when reply-to

            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var ops = siteAddresses.Select(a => new TransportOperation(outgoingMessage, new UnicastAddressTag(a))).ToArray();
            return tunnelDispatcher.Dispatch(new TransportOperations(ops), new TransportTransaction(), new ContextBag());
        }

        string ResolveAddress(string siteName)
        {
            string siteAddress;
            sites.TryGetValue(siteName, out siteAddress);
            return siteAddress;
        }

        Dictionary<string, string> sites;
        string thisSite;
    }
}