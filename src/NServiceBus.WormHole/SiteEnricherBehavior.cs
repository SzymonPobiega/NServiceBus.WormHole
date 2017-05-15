using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

namespace NServiceBus.WormHole
{
    using System.Linq;

    class SiteEnricherBehavior : Behavior<IOutgoingSendContext>
    {
        Dictionary<Type, Func<object, string[]>> siteMap;

        public SiteEnricherBehavior(Dictionary<Type, Func<object, string[]>> siteMap)
        {
            this.siteMap = siteMap;
        }

        public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
        {
            Func<object, string[]> siteCallback;
            if (siteMap.TryGetValue(context.Message.MessageType, out siteCallback))
            {
                var sites = siteCallback(context.Message.Instance);
                if (sites.Any(s => s.Contains(";")))
                {
                    throw new Exception("Site name cannot contain a semicolon.");
                }
                context.Headers["NServiceBus.WormHole.DestinationSites"] = string.Join(";", sites);
            }
            return next();
        }
    }
}