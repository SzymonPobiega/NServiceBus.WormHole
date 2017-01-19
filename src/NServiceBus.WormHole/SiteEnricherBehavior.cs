using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

namespace NServiceBus.WormHole
{
    class SiteEnricherBehavior : Behavior<IOutgoingSendContext>
    {
        Dictionary<Type, HashSet<string>> siteMap;

        public SiteEnricherBehavior(Dictionary<Type, HashSet<string>> siteMap)
        {
            this.siteMap = siteMap;
        }

        public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
        {
            HashSet<string> sites;
            if (siteMap.TryGetValue(context.Message.MessageType, out sites))
            {
                context.Headers["NServiceBus.WormHole.DestinationSites"] = string.Join(";", sites);
            }
            return next();
        }
    }
}