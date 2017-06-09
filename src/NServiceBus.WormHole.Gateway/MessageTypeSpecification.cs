namespace NServiceBus.Wormhole.Gateway
{
    public abstract class MessageTypeSpecification
    {
        public bool Overlaps(MessageTypeSpecification spec)
        {
            return spec.Overlaps(this);
        }

        public abstract bool OverlapsWith(MessageTypeRange spec);
        public abstract bool OverlapsWith(MessageType spec);
    }
}