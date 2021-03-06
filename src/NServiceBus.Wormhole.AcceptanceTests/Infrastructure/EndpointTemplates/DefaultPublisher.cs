namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;

    public class DefaultPublisher : IEndpointSetupTemplate
    {
#pragma warning disable CS0618
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
#pragma warning restore CS0618
        {
            return new DefaultServer().GetConfiguration(runDescriptor, endpointConfiguration, configurationBuilderCustomization);
        }
    }
}
