using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Wormhole;
using NServiceBus.Wormhole.Gateway;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_destination_gateway_is_not_configured : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_the_message()
    {
        var result = await Scenario.Define<Context>()
            .WithComponent(new GatewayComponent("SiteA", c =>
            {
                c.ErrorQueue = Conventions.EndpointNamingConvention(typeof(ErrorSpy));
            }))
            .WithComponent(new GatewayComponent("SiteB", c =>
            {
                c.ForwardToEndpoint(MessageType.Parse(typeof(MyMessage).AssemblyQualifiedName), 
                    Conventions.EndpointNamingConvention(typeof(Receiver)));
            }))
            .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyMessage())))
            .WithEndpoint<Receiver>()
            .WithEndpoint<ErrorSpy>()
            .Done(c => c.ErrorMessage != null)
            .Run();

        Assert.IsFalse(result.Received);
        Assert.AreEqual("Cannot resolve addresses of one or more sites: SiteB.", result.ErrorMessage);
    }

    class Context : ScenarioContext
    {
        public string ErrorMessage { get; set; }
        public bool Received { get; set; }
    }

    class Sender : EndpointConfigurationBuilder
    {
        public Sender()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.UseWormholeGateway("SiteA").RouteToSite<MyMessage>("SiteB");
            });
        }
    }

    class ErrorSpy : EndpointConfigurationBuilder
    {
        public ErrorSpy()
        {
            EndpointSetup<DefaultServer>();
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            Context scenarioContext;

            public MyMessageHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                scenarioContext.ErrorMessage = context.MessageHeaders["NServiceBus.ExceptionInfo.Message"];
                return Task.CompletedTask;
            }
        }
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver()
        {
            EndpointSetup<DefaultServer>();
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            Context scenarioContext;

            public MyMessageHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                scenarioContext.Received = true;
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : IMessage
    {
    }
}
