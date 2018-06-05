namespace Microsoft.Dynamics.Dynamics365Bot.Dialogs
{
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Dynamics.BotFramework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Dialog to display view to user.
    /// </summary>
    public class DisplayViewDialog : IDialog<object>
    {
        private readonly CrmClient crmClient = default;
        private readonly IDialogFactory dialogFactory = default;
        private LuisResult luisResult = default;

        /// <summary>
        /// Number of records to be displayed.
        /// </summary>
        public int NumberOfRecordsToDisplay { get; set; } = 10;

        string logicalName = string.Empty;
        string viewType = string.Empty;

        /// <summary>
        /// Initialize DisplayViewDialog.
        /// </summary>
        /// <param name="crmClient">CRM client</param>
        /// <param name="dialogFactory">Dialog factory</param>
        /// <param name="luisResult">LUIS result</param>
        public DisplayViewDialog(CrmClient crmClient, IDialogFactory dialogFactory, LuisResult luisResult)
        {
            this.crmClient = crmClient;
            this.dialogFactory = dialogFactory;
            this.luisResult = luisResult;
        }

        /// <summary>
        /// Starts interaction with the user.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <returns>Awaitable task.</returns>
        public async Task StartAsync(IDialogContext context)
        {
            this.logicalName = luisResult.Entities.Where(entity => entity.Type.Equals(RootLuisDialog.EntityTypeKey)).Select(entity => entity.Resolution.FirstOrDefault().Value as List<object>).FirstOrDefault()?.FirstOrDefault() as string;

            this.viewType = luisResult.Entities.Where(entity => entity.Type.Equals(RootLuisDialog.ViewStatusKey)).Select(entity => entity.Entity).FirstOrDefault();

            if (string.IsNullOrEmpty(this.logicalName))
            {
                await PromptForLogicalName(context);
            }
            else if (string.IsNullOrEmpty(this.viewType))
            {
                await PromptForViewType(context);
            }
            else
            {
                context.Call(this.dialogFactory.CreateGetViewRecordsDialog(this.logicalName, this.viewType), ResumeAfterRecordsAsync);
            }
        }

        /// <summary>
        /// Prompt for view type.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <returns>Task.</returns>
        private Task PromptForViewType(IDialogContext context)
        {
            PromptDialog.Text(context, ResumeAfterReceivingText, $"Which view for {this.logicalName} would you like for me to fetch (all active, my closed etc)?");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Prompt for logical name.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <returns>Task.</returns>
        private Task PromptForLogicalName(IDialogContext context)
        {
            PromptDialog.Text(context, ResumeAfterReceivingText, "I didn't catch the entity name, could you please provide logical name for the entity");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Resume after receiving text.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="result">Message sent by the user</param>
        /// <returns>Awaitable task.</returns>
        private async Task ResumeAfterReceivingText(IDialogContext context, IAwaitable<string> result)
        {
            var text = await result;

            if (string.IsNullOrEmpty(this.logicalName))
            {
                this.logicalName = text;
            }
            else
            {
                this.viewType = text;
            }

            if (string.IsNullOrEmpty(this.viewType))
            {
                await PromptForViewType(context);
                return;
            }

            context.Call(this.dialogFactory.CreateGetViewRecordsDialog(this.logicalName, this.viewType), ResumeAfterRecordsAsync);
        }

        /// <summary>
        /// Resume after fetching records.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="result">Message sent by the user</param>
        /// <returns>Awaitable task.</returns>
        private async Task ResumeAfterRecordsAsync(IDialogContext context, IAwaitable<(IView, IEnumerable<IEntity>)> result)
        {
            try
            {
                var (view, records) = await result;
                if (records.Count() == 0)
                {
                    await context.PostAsync($"No records found in {view.GetFieldValue<string>("name")}.");
                    context.Done(true);
                    return;
                }

                await context.PostAsync($"Displaying {this.NumberOfRecordsToDisplay} records only.");
                var reply = context.MakeMessage();
                reply.Attachments = reply.Attachments.Concat(await this.crmClient.RenderViewAsync(records.Take(this.NumberOfRecordsToDisplay).ToList(), view)).ToList();
                await context.PostAsync(reply);
                context.Done(true);
            }
            catch (NoViewFoundException exception)
            {
                await context.PostAsync(exception.Message);
                await context.PostAsync("Please try more specific query.");
                context.Done(false);
            }
            catch (Exception exception)
            {
                context.Fail(exception);
                //await context.PostAsync("Something went wrong. Please try again later.");
                //context.Done(false);
            }
        }
    }
}