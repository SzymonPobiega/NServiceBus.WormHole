﻿namespace NServiceBus.Wormhole.Gateway
{
    using System;

    public abstract class DistributionStrategy
    {
        /// <summary>
        /// Creates a new <see cref="DistributionStrategy"/>.
        /// </summary>
        /// <param name="endpoint">The name of the endpoint this distribution strategy resolves instances for.</param>
        /// <param name="scope">The scope for this strategy.</param>
        protected DistributionStrategy(string endpoint, DistributionStrategyScope scope)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            Scope = scope;
        }

        /// <summary>
        /// Selects a destination instance for a message from all known addresses of a logical endpoint.
        /// </summary>
        public abstract string SelectDestination(string[] candidates);

        /// <summary>
        /// The name of the endpoint this distribution strategy resolves instances for.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// The scope of this strategy.
        /// </summary>
        public DistributionStrategyScope Scope { get; }
    }
}