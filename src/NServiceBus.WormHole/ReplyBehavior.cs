namespace NServiceBus.Wormhole
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;
    using Transport;

    class ReplyBehavior : Behavior<IOutgoingReplyContext>
    {
        public override Task Invoke(IOutgoingReplyContext context, Func<Task> next)
        {
            string ultimateReplyTo;
            string sourceSite;

            IncomingMessage incomingMessage;
            if (!context.TryGetIncomingPhysicalMessage(out incomingMessage))
            {
                return next();
            }

            if (incomingMessage.Headers.TryGetValue("NServiceBus.Wormhole.SourceSite", out sourceSite)
                && incomingMessage.Headers.TryGetValue("NServiceBus.Wormhole.ReplyToAddress", out ultimateReplyTo))
            {
                context.Headers["NServiceBus.Wormhole.DestinationSites"] = sourceSite;
                context.Headers["NServiceBus.Wormhole.Destination"] = ultimateReplyTo;
            }
            return next();
        }
    }
}