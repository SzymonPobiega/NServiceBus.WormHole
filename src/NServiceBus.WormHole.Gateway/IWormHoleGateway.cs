using System.Threading.Tasks;

namespace NServiceBus.Wormhole.Gateway
{
    /// <summary>
    /// Represents a running worm hole gateway.
    /// </summary>
    public interface IWormholeGateway
    {
        Task Stop();
    }
}