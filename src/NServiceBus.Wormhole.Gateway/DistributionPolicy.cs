namespace NServiceBus.Wormhole.Gateway
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Allows configuring distribution strategies for endpoints.
    /// </summary>
    public class DistributionPolicy
    {
        private ConcurrentDictionary<Tuple<string, DistributionStrategyScope>, DistributionStrategy> configuredStrategies = new ConcurrentDictionary<Tuple<string, DistributionStrategyScope>, DistributionStrategy>();

        /// <summary>Sets the distribution strategy for a given endpoint.</summary>
        /// <param name="distributionStrategy">Distribution strategy to be used.</param>
        public void SetDistributionStrategy(DistributionStrategy distributionStrategy)
        {
            if (distributionStrategy == null)
            {
                throw new ArgumentNullException(nameof(distributionStrategy));
            }
            this.configuredStrategies[Tuple.Create(distributionStrategy.Endpoint, distributionStrategy.Scope)] = distributionStrategy;
        }

        internal DistributionStrategy GetDistributionStrategy(string endpointName, DistributionStrategyScope scope)
        {
            return configuredStrategies.GetOrAdd(Tuple.Create(endpointName, scope), key => new SingleInstanceRoundRobinDistributionStrategy(key.Item1, key.Item2));
        }
    }
}