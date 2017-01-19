using System.Threading.Tasks;

namespace NServiceBus.WormHole.Gateway
{
    public interface IWormHoleGateway
    {
        Task Stop();
    }
}