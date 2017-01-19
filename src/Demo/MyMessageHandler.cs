using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;

namespace Demo
{
    class MyMessageHandler : IHandleMessages<MyMessage>
    {
        public ReadOnlySettings Settings { get; set; }

        public Task Handle(MyMessage message, IMessageHandlerContext context)
        {
            Console.WriteLine($"{Settings.EndpointName()}: Got message");
            return context.Reply(new MyResponse());
        }
    }
}