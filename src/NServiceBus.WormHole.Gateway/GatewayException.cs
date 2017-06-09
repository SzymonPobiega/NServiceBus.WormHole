namespace NServiceBus.Wormhole.Gateway
{
    using System;

    /// <summary>
    /// Represents error in the gateway processing logic.
    /// </summary>
    public class GatewayException : Exception
    {
        public GatewayException(string reason) : base(reason)
        {
        }
    }
}