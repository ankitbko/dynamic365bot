namespace Microsoft.Dynamics.Dynamics365Bot.Scorables
{
    using BotAuth;
    using BotAuth.AADv1;
    using BotAuth.Dialogs;
    using BotAuth.Models;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Dialogs.Internals;
    using Microsoft.Bot.Builder.Scorables.Internals;
    using Microsoft.Bot.Connector;
    using System.Configuration;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Intercepts messages to make sure user is authenticated.
    /// </summary>
    public class AuthenticationScorable : ScorableBase<IActivity, string, double>
    {
        private IBotData _botData = null;
        private IDialogTask _dialogTask = null;
        private readonly AuthenticationOptions authOptions;
        private readonly IAuthProvider authProvider;

        /// <summary>
        /// Initialize authentication scorable.
        /// </summary>
        /// <param name="botdata">Bot data</param>
        /// <param name="dialogTask">Dialog task</param>
        /// <param name="botToUser">Bot to user</param>
        /// <param name="resourceId">Azure active directory resource identifier.</param>
        public AuthenticationScorable(IBotData botdata, IDialogTask dialogTask, IBotToUser botToUser, AuthenticationOptions authOptions, IAuthProvider authProvider)
        {
            _botData = botdata;
            _dialogTask = dialogTask;
            this.authOptions = authOptions;
            this.authProvider = authProvider;
        }

        /// <summary>
        /// Return completion of task.
        /// </summary>
        /// <param name="item">Activity item</param>
        /// <param name="state">State string</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Task.</returns>
        protected override Task DoneAsync(IActivity item, string state, CancellationToken token)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get score from an item and state.
        /// </summary>
        /// <param name="item">Activity item</param>
        /// <param name="state">State string</param>
        /// <returns>Double.</returns>
        protected override double GetScore(IActivity item, string state)
        {
            return 0.9; // 0.9 so that DeleteProfileScorable wins.
        }

        /// <summary>
        /// Whether the state has a score.
        /// </summary>
        /// <param name="item">Activity item</param>
        /// <param name="state">State string</param>
        /// <returns>True if the state has a score, false if it does not.</returns>
        protected override bool HasScore(IActivity item, string state)
        {
            //bool authStarted = false;

            //if (!string.IsNullOrEmpty(state))
            //    return !_botData.PrivateConversationData.TryGetValue("AuthenticationStarted", out bool _);
            //else
            //    return true;
            return string.IsNullOrEmpty(state);
        }

        /// <summary>
        /// Post to user.
        /// </summary>
        /// <param name="item">Item activity</param>
        /// <param name="state">State string</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Awaitable task.</returns>
        protected override async Task PostAsync(IActivity item, string state, CancellationToken token)
        {
            var dialog = new AuthDialog(this.authProvider, this.authOptions);

            _botData.PrivateConversationData.SetValue("AuthenticationStarted", true);

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            var interruption = dialog
                .Do(async (context, result) => context.PrivateConversationData.RemoveValue("AuthenticationStarted"))
                .ContinueWith(async (context, result) => Chain.Return(item));
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

            await _dialogTask.Forward(interruption, null, item, token);

            await _dialogTask.PollAsync(token);
        }

        /// <summary>
        /// Prepare by getting access token from Azure Active Directory.
        /// </summary>
        /// <param name="item">Activity item</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Token string as an awaitable task.</returns>
        protected override async Task<string> PrepareAsync(IActivity item, CancellationToken token)
        {
            return await _botData.GetTokenAsync(this.authOptions, this.authProvider, token);
        }
    }
}