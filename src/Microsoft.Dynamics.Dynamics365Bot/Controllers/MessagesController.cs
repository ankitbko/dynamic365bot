namespace Microsoft.Dynamics.Dynamics365Bot
{
    using Autofac;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Dialogs.Internals;
    using Microsoft.Bot.Connector;
    using Microsoft.Dynamics.Dynamics365Bot.Dialogs;
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Hosting;
    using System.Web.Http;

    /// <summary>
    /// Controller that interacts with the user by accepting a message and replying back appropriately. 
    /// </summary>
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private static int TypingDelay = 3000;
        private static int TypingTimeout = 2 * 60 * 1000;
        
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                HostingEnvironment.QueueBackgroundWorkItem(async (token) => await this.SendToBot(activity, token));
            }
            else
            {
                await HandleSystemMessageAsync(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        /// <summary>
        /// Handle message activity type.
        /// </summary>
        /// <param name="activity">Activity</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Awaitable task.</returns>
        private async Task SendToBot(Activity activity, CancellationToken cancellationToken)
        {
            using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, activity))
            {
                DialogModule_MakeRoot.Register(scope, () => scope.Resolve<RootLuisDialog>());
                var postTask = scope.Resolve<IPostToBot>().PostAsync(activity, cancellationToken);
                await this.SendTypingUntillTaskComplete(postTask, activity);
                await postTask;
            }
        }

        /// <summary>
        /// Handle system message activity types.
        /// </summary>
        /// <param name="message">Activity message</param>
        /// <returns>Activity as an awaitable task.</returns>
        private async Task<Activity> HandleSystemMessageAsync(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
                if (message.MembersAdded.Any(member => member.Id == message.Recipient.Id))
                {
                    ConnectorClient connector = new ConnectorClient(new Uri(message.ServiceUrl));
                    var reply = message.CreateReply(Resource.Resources.WelcomeMessage);
                    await connector.Conversations.ReplyToActivityAsync(reply);

                    reply = message.CreateReply();
                    reply = Response.HelpMessage(reply);
                    await connector.Conversations.ReplyToActivityAsync(reply);
                }
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing that the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }

        /// <summary>
        /// Send typing message to user until task is completed.
        /// </summary>
        /// <param name="runningTask">Running task</param>
        /// <param name="message">Activity message</param>
        /// <returns>Awaitable task.</returns>
        private async Task SendTypingUntillTaskComplete(Task runningTask, Activity message)
        {
            ConnectorClient connector = new ConnectorClient(new Uri(message.ServiceUrl));
            var reply = message.CreateReply();
            reply.Type = ActivityTypes.Typing;
            var counter = 0;

            while (!(runningTask.IsCompleted || counter >= MessagesController.TypingTimeout))
            {
                await connector.Conversations.ReplyToActivityAsync(reply);
                await Task.Delay(MessagesController.TypingDelay);
                counter += MessagesController.TypingDelay;
            }
        }
    }
}