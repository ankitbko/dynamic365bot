namespace Microsoft.Dynamics.Dynamics365Bot
{
    using BotAuth;
    using BotAuth.AADv1;
    using BotAuth.Models;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Dialogs.Internals;
    using Microsoft.Bot.Connector;
    using Microsoft.Dynamics.BotFramework;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Generalized extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Split a large enumerable into smaller chunks
        /// Depending on the size of the chunks as specified.
        /// </summary>
        /// <typeparam name="T">Generic type of collection</typeparam>
        /// <param name="enumerable">Enumerable</param>
        /// <param name="chunkSize">Chunk size</param>
        /// <returns>Enumerable of enumerable of smaller chunks.</returns>
        public static IEnumerable<IEnumerable<T>> Chunks<T>(this IEnumerable<T> enumerable, int chunkSize)
        {
            // https://stackoverflow.com/questions/419019/split-list-into-sublists-with-linq/20953521#20953521
            if (chunkSize < 1) throw new ArgumentException("chunkSize must be positive");

            using (var enumerator = enumerable.GetEnumerator())
                while (enumerator.MoveNext())
                {
                    var remaining = chunkSize;    // elements remaining in the current chunk
                    var innerMoveNext = new Func<bool>(() => --remaining > 0 && enumerator.MoveNext());

                    yield return enumerator.GetChunk(innerMoveNext);
                    while (innerMoveNext()) {/* discard elements skipped by inner iterator */}
                }
        }
        
        /// <summary>
        /// Get access token for accessing resource.
        /// </summary>
        /// <param name="botData">Bot data</param>
        /// <param name="resourceId">Resource ID</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Access token as an awaitable task.</returns>
        public async static Task<string> GetTokenAsync(this IBotData botData, AuthenticationOptions authOptions, IAuthProvider authProvider, CancellationToken token)
        {
            AuthResult authResult = default;
            if (botData.UserData.TryGetValue($"{authProvider.Name}{ContextConstants.AuthResultKey}", out authResult))
            {
                try
                {
                    InMemoryTokenCacheADAL tokenCache = new InMemoryTokenCacheADAL(authResult.TokenCache);

                    AuthenticationContext authContext = new AuthenticationContext(authOptions.Authority, tokenCache);
                    var result = await authContext.AcquireTokenSilentAsync(authOptions.ResourceId,
                        new ClientCredential(authOptions.ClientId, authOptions.ClientSecret),
                        new UserIdentifier(authResult.UserUniqueId, UserIdentifierType.UniqueId));
                    authResult = result.FromADALAuthenticationResult(tokenCache);
                    await botData.StoreAuthResultAsync(authResult, authProvider, token);
                }
                catch (Exception)
                {
                    return null;
                }
                return authResult.AccessToken;
            }
            return null;
        }

        /// <summary>
        /// Set the authentication result key as a part of user data in Bot data.
        /// </summary>
        /// <param name="botData">Bot data</param>
        /// <param name="authResult">Authentication result</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Task.</returns>
        public static Task StoreAuthResultAsync(this IBotData botData, AuthResult authResult, IAuthProvider authProvider, CancellationToken token)
        {
            botData.UserData.SetValue($"{authProvider.Name}{ContextConstants.AuthResultKey}", authResult);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Remove all authentication data from user data saved as user data in bot data
        /// </summary>
        /// <param name="botData">Bot data</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Task</returns>
        public static Task LogoutAsync(this IBotData botData, IAuthProvider authProvider, CancellationToken token)
        {
            botData.UserData.RemoveValue($"{authProvider.Name}{ContextConstants.AuthResultKey}");
            botData.UserData.RemoveValue($"{authProvider.Name}{ContextConstants.MagicNumberKey}");
            botData.UserData.RemoveValue($"{authProvider.Name}{ContextConstants.MagicNumberValidated}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Post response to the user.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="responses">List of responses</param>
        /// <returns>Awaitable task.</returns>
        public static async Task PostAsync(this IDialogContext context, List<string> responses)
        {
            await context.PostAsync(responses.OrderBy(x => Guid.NewGuid()).FirstOrDefault());
        }

        /// <summary>
        /// Get chunk of generic type T.
        /// </summary>
        /// <typeparam name="T">Generic type</typeparam>
        /// <param name="enumerator">Enumerator</param>
        /// <param name="innerMoveNext">Inner move next function</param>
        /// <returns>IEnumerable of T.</returns>
        private static IEnumerable<T> GetChunk<T>(this IEnumerator<T> enumerator, Func<bool> innerMoveNext)
        {
            do yield return enumerator.Current;
            while (innerMoveNext());
        }

        public static void StoreEntityReference(this IBotDataBag botDataBag, IEntity entity, string key= "LastEntityReference")
        {
            var serializerSetting = new JsonSerializerSettings() { ContractResolver = new InterfaceContractResolver<IEntityReference>() };
            botDataBag.SetValue(key, JsonConvert.SerializeObject(entity, serializerSetting));
        }
    }
}