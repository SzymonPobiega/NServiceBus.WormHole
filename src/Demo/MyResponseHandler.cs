using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;

namespace Demo
{
    class MyResponseHandler : IHandleMessages<MyResponse>
    {
        public ReadOnlySettings Settings { get; set; }

        public Task Handle(MyResponse message, IMessageHandlerContext context)
        {
            Console.WriteLine($"{Settings.EndpointName()}: Got response");
            return Task.CompletedTask;
        }
    }
}