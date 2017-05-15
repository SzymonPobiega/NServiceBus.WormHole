using System.Threading.Tasks;

namespace NServiceBus.WormHole.Gateway
{
    /// <summary>
    /// Represents a running worm hole gateway.
    /// </summary>
    public interface IWormHoleGateway
    {
        Task Stop();
    }
}