namespace Microsoft.Dynamics.Dynamics365Bot
{
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.Azure.Services.AppAuthentication;
    using System.Configuration;
    using System.Threading.Tasks;

    /// <summary>
    /// Helper class to access and read from the config file.
    /// </summary>
    internal static class ConfigHelper
    {
		/// <summary>
		/// The organization URL key on the config file.
		/// </summary>
		public const string KeyOrganizationUrl = "OrganizationUrl";

		/// <summary>
		/// The Activity Directory Resource ID key on the config file.
		/// </summary>
		public const string KeyADResourceId = "ActiveDirectoryResourceId";

		public const string KeyADEndpointUrl = "ActiveDirectoryEndpointUrl";

        /// <summary>
        /// The Activity Directory Client ID key on the config file.
        /// </summary>
        public const string KeyADClientId = "ActiveDirectoryClientId";

        public const string KeyADClientSecret = "ActiveDirectoryClientSecret";

        public const string KeyRedirectUrl = "ActiveDirectoryRedirectUrl";

        public const string KeyTenant = "ActiveDirectoryTenant";
        
        /// <summary>
        /// Key API version.
        /// </summary>
        public const string KeyApiVersion = "CrmApiVersion";

        public const string KeyKeyVaultResourceId = "KeyVaultResourceId";

        public const string KeyLuisModelId = "LuisModelId";

        public const string KeyLuisSubscriptionKey = "LuisSubscriptionKey";

        public const string KeyWebChatSecret = "WebChatSecret";

        public const string KeyStorageAccount = "StorageAccountConnectionString";

        /// <summary>
        /// Reads a key on the config file and return its value (or a default if it can't be read).
        /// </summary>
        /// <param name="key">The key to be read.</param>
        /// <param name="defaultValue">The default value in case the key can't be read.</param>
        /// <returns>Value of the key.</returns>
        public static string Read(string key, string defaultValue = null)
        {
			if (defaultValue == null)
			{
				defaultValue = string.Empty;
			}
            string value = defaultValue;
            try
            {
                value = ConfigurationManager.AppSettings[key].ToString();
            }
            catch
            {
                value = defaultValue;
            }
            return value;
        }

        public static Task<SecretBundle> GetSecretFromKeyVaultAsync(string azureAdInstance, string keyVaultResourceId, string secretKey)
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider(azureAdInstance: azureAdInstance);
            var keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

            return keyVaultClient.GetSecretAsync($"{keyVaultResourceId}/{secretKey}");
        }
    }
}