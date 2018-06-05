namespace Microsoft.Dynamics.Dynamics365Bot
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Json.Net contract resolver to serialize object based on exposed properties on its interface.
    /// </summary>
    /// <typeparam name="TInterface">Generic interface</typeparam>
    public class InterfaceContractResolver<TInterface> : DefaultContractResolver where TInterface : class
    {
        /// <summary>
        /// Create properties.
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="memberSerialization">Member serialization</param>
        /// <returns>IList of JSON properties.</returns>
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> properties = base.CreateProperties(typeof(TInterface), memberSerialization);
            return properties;
        }
    }
}