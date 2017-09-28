namespace NServiceBus.Wormhole
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Configuration.AdvancedExtensibility;
    using Settings;

    /// <summary>
    /// Configures wormhole routing.
    /// </summary>
    public class WormholeRoutingSettings : ExposeSettings
    {
        internal WormholeRoutingSettings(string gatwayAddress, SettingsHolder settings) : base(settings)
        {
            GatwayAddress = gatwayAddress;
        }

        internal string GatwayAddress { get; }
        internal Dictionary<Type, Func<object, string[]>> RouteTable { get; } = new Dictionary<Type, Func<object, string[]>>();

        /// <summary>
        /// Configures routing of a given message type to the provided sites via the worm hole.
        /// </summary>
        /// <param name="sitesProperty">The property of message that contains the names of the destination sites.</param>
        public WormholeRoutingSettings RouteToSite<T>(Func<T, IEnumerable<string>> sitesProperty)
        {
            string[] routeCallback(object o)
            {
                var message = (T)o;
                return sitesProperty(message).ToArray();
            }

            if (sitesProperty == null)
            {
                throw new ArgumentNullException(nameof(sitesProperty));
            }

            RouteTable[typeof(T)] = routeCallback;
            return this;
        }

        /// <summary>
        /// Configures routing of a given message type to the provided site via the worm hole.
        /// </summary>
        /// <param name="siteProperty">The property of message that contains the name of the destination site.</param>
        public WormholeRoutingSettings RouteToSite<T>(Func<T, string> siteProperty)
        {
            string[] routeCallback(object o)
            {
                var message = (T)o;
                return new[]
                {
                    siteProperty(message)
                };
            }

            if (siteProperty == null)
            {
                throw new ArgumentNullException(nameof(siteProperty));
            }

            RouteTable[typeof(T)] = routeCallback;
            return this;
        }

        /// <summary>
        /// Configures routing of a given message type to the provided site via the worm hole.
        /// </summary>
        /// <param name="destinationSite">The name of the destination site.</param>
        public WormholeRoutingSettings RouteToSite<T>(string destinationSite)
        {
            string[] routeCallback(object o) => new[]
            {
                destinationSite
            };

            if (destinationSite == null)
            {
                throw new ArgumentNullException(nameof(destinationSite));
            }

            RouteTable[typeof(T)] = routeCallback;
            return this;
        }
    }
}