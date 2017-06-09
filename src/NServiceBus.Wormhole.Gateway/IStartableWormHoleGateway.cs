namespace NServiceBus.Wormhole.Gateway
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a worm hole gateway instance ready to start.
    /// </summary>
    public interface IStartableWormholeGateway
    {
        Task<IWormholeGateway> Start();
    }
}