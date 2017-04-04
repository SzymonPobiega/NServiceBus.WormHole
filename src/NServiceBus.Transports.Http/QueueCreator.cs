namespace NServiceBus.Transports.Http
{
    using System.Threading.Tasks;
    using Transport;

    class QueueCreator : ICreateQueues
    {
        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            //TODO: This should probably create url ACLs.
            return Task.CompletedTask;
        }
    }
}