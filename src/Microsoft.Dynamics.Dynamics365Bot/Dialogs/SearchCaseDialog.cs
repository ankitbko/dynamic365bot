namespace Microsoft.Dynamics.Dynamics365Bot.Dialogs
{
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using Microsoft.Dynamics.BotFramework;
    using Microsoft.Dynamics.Dynamics365Bot.Resource;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a search dialog to the user for searching cases.
    /// </summary>
    [Serializable]
    public class SearchCaseDialog : IDialog<object>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2235:MarkAllNonSerializableFields", Justification = "CrmClient is marked in registration to never serialize by BotFramework")]
        private readonly CrmClient crmClient = default;
        private readonly IDialogFactory dialogFactory = default;
        private readonly string query = default;
        private readonly string attributeName = "ticketnumber";
        private const string EntityLogicalName = "incident";

        private const int CardFormCode = 11;
        private const int MainFormCode = 2;

        /// <summary>
        /// Initialize SearchCaseDialog.
        /// </summary>
        /// <param name="crmClient">CRM client for interacting with CRM</param>
        /// <param name="dialogFactory">Dialog factory</param>
        /// <param name="query">Query</param>
        public SearchCaseDialog(CrmClient crmClient, IDialogFactory dialogFactory, string query = "")
        {
            this.crmClient = crmClient;
            this.dialogFactory = dialogFactory;
            this.query = query;
        }

        /// <summary>
        /// Start interaction with the user.
        /// </summary>
        /// <param name="context">Dialog context.</param>
        /// <returns>Awaitable task.</returns>
        public Task StartAsync(IDialogContext context)
        {
            var dialog = this.dialogFactory.CreateRecordSearchDialog(SearchCaseDialog.EntityLogicalName, attributeName: this.attributeName, query: this.query);
            context.Call(dialog, this.ResumeAfterSearchAsync);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Resume after search.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="result">Message sent by the user</param>
        /// <returns>Awaitable task.</returns>
        private async Task ResumeAfterSearchAsync(IDialogContext context, IAwaitable<IEnumerable<IEntity>> result)
        {
            try
            {
                var entities = (await result).ToList();
                if (entities.Count == 0)
                {
                    await context.PostAsync("No records found.");
                    context.Done(false);
                    return;
                }
                else if (entities.Count > 1)
                {
                    context.Call(this.dialogFactory.CreateChooseRecordDialog(entities), this.ResumeAfterChooseDialogAsync);
                }
                else
                {
                    await this.ResumeAfterEntitySelectionAsync(context, entities.First());
                }
            }
            catch (Exception exception)
            {
                context.Fail(exception);
            }
        }

        /// <summary>
        /// Resume after choose dialog.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="result">Message sent by the user</param>
        /// <returns>Awaitable task.</returns>
        private async Task ResumeAfterChooseDialogAsync(IDialogContext context, IAwaitable<IEntityReference> result)
        {
            var reference = await result;
            var record = await this.crmClient.RetrieveRecordAsync(reference);
            await this.ResumeAfterEntitySelectionAsync(context, record);
        }

        /// <summary>
        /// Resume after entity selection.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="record">Entity record</param>
        /// <returns>Awaitable task.</returns>
        private async Task ResumeAfterEntitySelectionAsync(IDialogContext context, IEntity record)
        {
            try
            {
                context.PrivateConversationData.StoreEntityReference(record);

                await context.PostAsync(Resources.SearchCase_ProcessingEntity);

                var form = (await this.crmClient
                    .RetrieveFormsOf(record.LogicalName, SearchCaseDialog.CardFormCode))
                    .Where(entity => entity.GetFieldValue("name", string.Empty).Equals("Case Card", StringComparison.OrdinalIgnoreCase));

                var reply = context.MakeMessage();

                if (form.Count() == 0)
                {
                    // Try get main form
                    form = await this.crmClient.RetrieveFormsOf(record.LogicalName, SearchCaseDialog.MainFormCode);
                }

                if (form.Count() == 0)
                {
                    await context.PostAsync("No form found to display result.");
                    context.Done(false);
                    return;
                }

                reply.Attachments.Add(await this.crmClient.RenderReadOnlyFormAsync(record, form.First()));

                reply.SuggestedActions = new SuggestedActions
                {
                    Actions = new List<CardAction>
                    {
                        new CardAction {Title = "Show me related email records", Value="Show me related email records", Type= ActionTypes.ImBack},
                        new CardAction {Title = "Show me related hold activities", Value="Show me related hold activities", Type= ActionTypes.ImBack},
                        new CardAction {Title = "Edit this case", Value="Edit this case", Type= ActionTypes.ImBack}
                    }
                };
                await context.PostAsync(reply);

                context.Done(true);
            }
            catch (Exception exception)
            {
                context.Fail(exception);
            }
        }
    }
}