using NServiceBus;

namespace Demo
{
    class MyMessage : IMessage
    {
        public string Destination { get; set; }
    }
}