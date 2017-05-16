namespace NServiceBus.WormHole
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

            if (incomingMessage.Headers.TryGetValue("NServiceBus.WormHole.SourceSite", out sourceSite)
                && incomingMessage.Headers.TryGetValue("NServiceBus.WormHole.ReplyToAddress", out ultimateReplyTo))
            {
                context.Headers["NServiceBus.WormHole.DestinationSites"] = sourceSite;
                context.Headers["NServiceBus.WormHole.Destination"] = ultimateReplyTo;
            }
            return next();
        }
    }
}