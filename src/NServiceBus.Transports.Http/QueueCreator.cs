namespace NServiceBus.Transports.Http
{
    using System.Security.Principal;
    using System.Threading.Tasks;
    using ServiceControlInstaller.Engine.UrlAcl;
    using Transport;

    class QueueCreator : ICreateQueues
    {
        public QueueCreator(AddressParser addressParser)
        {
            this.addressParser = addressParser;
        }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            SecurityIdentifier sid;
            if (identity == null)
            {
                sid = WindowsIdentity.GetCurrent().User;
            }
            else
            {
                var account = new NTAccount(identity);
                sid = (SecurityIdentifier) account.Translate(typeof(SecurityIdentifier));
            }

            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                var parsedAddress = addressParser.ParseAddress(receivingAddress);
                var reservation = new UrlReservation(parsedAddress + "/", sid);
                reservation.Create();
            }

            return Task.CompletedTask;
        }

        AddressParser addressParser;
    }
}