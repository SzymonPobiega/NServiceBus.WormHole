namespace NServiceBus.WormHole.Gateway
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a worm hole gateway instance ready to start.
    /// </summary>
    public interface IStartableWormHoleGateway
    {
        Task<IWormHoleGateway> Start();
    }
}