namespace Microsoft.Dynamics.Dynamics365Bot
{
    using BotAuth;
    using BotAuth.Models;
    using Microsoft.Bot.Builder.Dialogs.Internals;
    using Microsoft.Dynamics.BotFramework;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Mechanism to authenticate and retrieve valid tokens to a CRM entity in use using the Microsoft Identity Model.
    /// </summary>
    [Serializable]
    public class CrmAuthenticator : ICrmAuthenticator
    {
        private IBotData botData = default;
        private readonly AuthenticationOptions authOptions;
        private readonly IAuthProvider authProvider;

        /// <summary>
        /// Mechanism to authenticate and retrieve valid tokens to a CRM entity in use using the Microsoft Identity Model.
        /// </summary>
        /// <param name="botData">IBotData instance for user in context</param>
        /// <param name="resourceId">Resource Id (URL) for the CRM system.</param>
        public CrmAuthenticator(IBotData botData, AuthenticationOptions authOptions, IAuthProvider authProvider)
        {
            this.botData = botData;
            this.authOptions = authOptions;
            this.authProvider = authProvider;
        }

        /// <summary>
        /// Get access token for accessing the resource.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="forceRefresh"></param>
        /// <returns>Access token as an awaitable Task.</returns>
        public async Task<string> GetToken(string key, bool forceRefresh)
        {
            var token = await botData.GetTokenAsync(this.authOptions, this.authProvider, CancellationToken.None);
            if (token == null)
            {
                await botData.LogoutAsync(this.authProvider, CancellationToken.None);
            }
            return token;
        }

        /// <summary>
        /// Whether or not the access token has expired.
        /// </summary>
        /// <param name="resourceId">Resource ID</param>
        /// <returns>Always false.</returns>
        public bool IsTokenExpired(string resourceId)
        {
            return false;
        }
    }
}