namespace Microsoft.Dynamics.Dynamics365Bot.Dialogs
{
    using BotAuth;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Connector;
    using Microsoft.Dynamics.BotFramework;
    using Microsoft.Dynamics.Dynamics365Bot.Resource;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interact with the user
    /// With the help of LUIS to understand the intent
    /// And render appropriate results.
    /// </summary>
    public class RootLuisDialog : LuisDialog<object>
    {
        internal const string EntityTypeKey = "EntityType";
        internal const string ViewTypeKey = "ViewType";
        internal const string ViewStatusKey = "ViewStatus";
        internal const string IncidentKey = "incident";
        internal const string OpportunityKey = "opportunity";
        private readonly CrmClient crmClient = default;
        private readonly ICache<string, List<IEntity>> entityCache = default;
        private readonly IDialogFactory dialogFactory = default;
        private readonly IAuthProvider authProvider;

        /// <summary>
        /// Initialize RootLuisDialog.
        /// </summary>
        /// <param name="client">CRM Client to interact with CRM</param>
        /// <param name="entityCache">List of cached entities</param>
        /// <param name="dialogFactory">Dialog factory</param>
        /// <param name="services">LUIS services</param>
        public RootLuisDialog(CrmClient client,
            ICache<string,
            List<IEntity>> entityCache,
            IDialogFactory dialogFactory,
            IAuthProvider authProvider,
            params ILuisService[] services) : base(services)
        {
            this.crmClient = client;
            this.entityCache = entityCache;
            this.dialogFactory = dialogFactory;
            this.authProvider = authProvider;
        }

        /// <summary>
        /// Open record if the intent detected by LUIS is such.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="message">Message</param>
        /// <param name="luisResult">LUIS result</param>
        /// <returns>Awaitable task.</returns>
        [LuisIntent("OpenRecord")]
        public async Task OpenRecord(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult luisResult)
        {
            var entityType = (luisResult.Entities.SingleOrDefault(e => e.Type.Equals(EntityTypeKey))?.Resolution["values"] as List<object>)?.FirstOrDefault()?.ToString() ?? string.Empty;
            if (entityType.Equals(RootLuisDialog.IncidentKey, System.StringComparison.OrdinalIgnoreCase))
            {
                var query = luisResult.Entities.SingleOrDefault(e => e.Type.Equals("Id"))?.Entity.Replace(" ", string.Empty);
                context.Call(new SearchCaseDialog(this.crmClient, dialogFactory, query: query), this.ResumeVoid);
                return;
            }
            else if (entityType.Equals(RootLuisDialog.OpportunityKey, System.StringComparison.OrdinalIgnoreCase))
            {
                var query = luisResult.Entities.FirstOrDefault(e => e.Type.Equals("AccountName"))?.Entity;
                context.Call(new SearchOpportunityDialog(this.crmClient, dialogFactory, accountName: query), this.ResumeVoid);
                return;
            }
            await context.PostAsync("Sorry, I did not understand you.");
            context.Wait(MessageReceived);
        }

        [LuisIntent("EditRecord")]
        public async Task EditRecord(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult luisResult)
        {
            await context.Forward(new EditEntityDialog(this.crmClient, luisResult), this.ResumeVoid, await message);
        }

        /// <summary>
        /// Open view if the intent detected by LUIS is such.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="message">Message</param>
        /// <param name="luisResult">LUIS result</param>
        /// <returns>Awaitable task.</returns>
        [LuisIntent("OpenView")]
        public Task OpenView(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult luisResult)
        {
            context.Call(new DisplayViewDialog(this.crmClient, dialogFactory, luisResult), this.ResumeVoid);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Open related records if the intent detected by LUIS is such.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="message">Message</param>
        /// <param name="luisResult">LUIS result</param>
        /// <returns>Awaitable task.</returns>
        [LuisIntent("OpenRelatedRecords")]
        public async Task OpenRelatedRecord(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult luisResult)
        {
            await context.Forward(new OpenRelatedRecordDialog(this.crmClient, luisResult), this.ResumeVoid, await message);
        }

        /// <summary>
        /// Search record by account name if the intent detected by LUIS is such.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="luisResult">LUIS result</param>
        /// <returns>Awaitable task.</returns>
        [LuisIntent("OpenCaseUsingAccountName")]
        public async Task SearchRecordByAccountName(IDialogContext context, LuisResult luisResult)
        {
            // TODO Unify this with OpenRecord
            var entityType = (luisResult.Entities.SingleOrDefault(e => e.Type.Equals(EntityTypeKey))?.Resolution["values"] as List<object>)?.FirstOrDefault()?.ToString() ?? string.Empty;
            var name = luisResult.Entities.SingleOrDefault(entity => entity.Type.Equals("AccountName"))?.Entity;

            if (entityType.Equals(RootLuisDialog.IncidentKey, System.StringComparison.OrdinalIgnoreCase))
            {
                context.Call(new SearchCaseByAccountNameDialog(this.crmClient, entityCache, name), this.ResumeVoid);
            }
            else if (entityType.Equals(RootLuisDialog.OpportunityKey, System.StringComparison.OrdinalIgnoreCase))
            {
                context.Call(new SearchOpportunityDialog(this.crmClient, dialogFactory, accountName: name), this.ResumeVoid);
            }
            else
            {
                await context.PostAsync("Sorry, I did not understand you.");
                context.Wait(MessageReceived);
            }
        }

        /// <summary>
        /// Greeting intnet
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="message">Message</param>
        /// <param name="luisResult">LUIS result</param>
        /// <returns>Awaitable Task.</returns>
        [LuisIntent("Greetings")]
        public async Task Greetings(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult luisResult)
        {
            await context.PostAsync("Hey there.");
            await Help(context, message, luisResult);
        }

        /// <summary>
        /// Greeting intnet
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="message">Message</param>
        /// <param name="luisResult">LUIS result</param>
        /// <returns>Awaitable Task.</returns>
        [LuisIntent("Help")]
        public async Task Help(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult luisResult)
        {
            var reply = context.MakeMessage() as Activity;
            reply = Response.HelpMessage(reply);
            await context.PostAsync(reply);
        }

        /// <summary>
        /// If the intent cannot be detected by LUIS.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="message">Message</param>
        /// <param name="luisResult">LUIS result</param>
        /// <returns>Awaitable Task.</returns>
        [LuisIntent("None")]
        [LuisIntent("")]
        public async Task None(IDialogContext context, IAwaitable<IMessageActivity> message, LuisResult luisResult)
        {
            await context.PostAsync(Resources.CannotUnderstand);
            await Help(context, message, luisResult);
        }


        /// <summary>
        /// Resume void.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="result">Message sent by the user</param>
        /// <returns>Task</returns>
        private async Task ResumeVoid(IDialogContext context, IAwaitable<object> result)
        {
            try
            {
                await result;
            }
            catch (AggregateException aggregateException)
            {
                var flattenedException = aggregateException.Flatten();
                if (flattenedException.InnerExceptions.Any(ex => ex is CrmWebApiException))
                {
                    await HandleCrmWebApiExceptionAsync(flattenedException.InnerExceptions.Where(ex => ex is CrmWebApiException).First() as CrmWebApiException, context);
                }
                else
                {
                    await context.PostAsync(flattenedException.InnerExceptions.First().GetBaseException().Message);
                    await context.PostAsync(Resources.GenericError);
                }
            }
            catch (CrmWebApiException webApiException)
            {
                await HandleCrmWebApiExceptionAsync(webApiException, context);
            }
            catch (IncorrectLogicalNameException incorrectLogicalNameException)
            {
                await context.PostAsync(incorrectLogicalNameException.Message);
            }
            catch (NoViewFoundException)
            {
                await context.PostAsync(Resources.NoViewFoundExceptionMessage);
            }
            catch (Exception exception)
            {
                await context.PostAsync(exception.GetBaseException().Message);
                await context.PostAsync(Resources.GenericError);
            }
            context.Wait(base.MessageReceived);

            async Task HandleCrmWebApiExceptionAsync(CrmWebApiException webApiException, IDialogContext dialogContext)
            {
                if (webApiException.Response != null)
                {
                    if (webApiException.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        await dialogContext.PostAsync(Resources.ForbiddenError);
                    }
                    else if (webApiException.Response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        await dialogContext.PostAsync(Resources.UnauthorizedError);
                        await context.LogoutAsync(this.authProvider, CancellationToken.None);
                    }
                    else
                    {
                        await dialogContext.PostAsync($"Received {webApiException.Response.StatusCode} status code.");
                        await context.PostAsync(Resources.GenericError);
                    }
                }
                else
                {
                    await dialogContext.PostAsync(webApiException.Message);
                    await context.PostAsync(Resources.GenericError);
                }
            }
        }
    }
}