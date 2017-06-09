using NServiceBus.Routing;

namespace NServiceBus.Wormhole.Gateway
{
    public class RoutingTableEntry
    {
        /// <summary>
        /// Message type filter expression.
        /// </summary>
        public MessageTypeSpecification MessageTypeSpec { get; }
        /// <summary>
        /// Route for the message type.
        /// </summary>
        public UnicastRoute Route { get; }

        /// <summary>
        /// Creates a new entry.
        /// </summary>
        public RoutingTableEntry(MessageTypeSpecification messageTypeSpec, UnicastRoute route)
        {
            MessageTypeSpec = messageTypeSpec;
            Route = route;
        }
    }
}