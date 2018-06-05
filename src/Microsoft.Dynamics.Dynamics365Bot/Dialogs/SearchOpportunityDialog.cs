using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.Dynamics.BotFramework;

namespace Microsoft.Dynamics.Dynamics365Bot.Dialogs
{
    internal class SearchOpportunityDialog : IDialog<object>
    {
        private static readonly object FilterAttributeName = "name";
        private static readonly string EntityLogicalName = "opportunity";
        private readonly string OpenStateCode = "0";
        private CrmClient crmClient;
        private IDialogFactory dialogFactory;
        private string accountName;

        public SearchOpportunityDialog(CrmClient crmClient, IDialogFactory dialogFactory, string accountName)
        {
            this.crmClient = crmClient;
            this.dialogFactory = dialogFactory;
            this.accountName = accountName;
        }

        /// <summary>
        /// Start interaction with the user.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <returns>Awaitable task.</returns>
        public async Task StartAsync(IDialogContext context)
        {
            if (string.IsNullOrEmpty(this.accountName))
            {
                await context.PostAsync("Enter account name");
                context.Wait(this.AccountNameReceivedAsync);
            }
            else
            {
                await SearchAsync(context, this.accountName);
            }
        }

        /// <summary>
        /// Handle received account name.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="result">Message sent by the user</param>
        /// <returns>Task</returns>
        private async Task AccountNameReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            this.accountName = message.Text;
            await this.SearchAsync(context, this.accountName);
        }

        private async Task SearchAsync(IDialogContext context, string accountName)
        {
            var opportunities = await this.FetchRecentOpportunitiesForAccount(accountName, true);
            if (opportunities.Count == 0)
            {
                await context.PostAsync($"No recent open opportunities for account {accountName}");
                context.Done(false);
            }
            else if (opportunities.Count > 1)
            {
                await context.PostAsync($"I found {opportunities.Count} opportunities for account {accountName}");
                context.Call(this.dialogFactory.CreateChooseRecordDialog(opportunities), this.ResumeAfterChooseDialogAsync);
            }
            else
            {
                await this.DisplayOpportunityAsync(context, opportunities.First());
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
            await this.DisplayOpportunityAsync(context, record);
        }

        /// <summary>
        /// Display incidents.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="opportunities">List of incidents</param>
        /// <param name="text">Reply text</param>
        /// <returns>Awaitable task.</returns>
        private async Task DisplayOpportunityAsync(IDialogContext context, IEntity record)
        {
            try
            {
                context.PrivateConversationData.StoreEntityReference(record);
                var form = await this.crmClient.RetrieveFormsOf(record.LogicalName, 2);

                if (form.Count() == 0)
                {
                    await context.PostAsync("No form found to display result.");
                    return;
                }

                var reply = context.MakeMessage();

                reply.Attachments.Add(await this.crmClient.RenderReadOnlyFormAsync(record, form.First()));

                reply.SuggestedActions = new SuggestedActions
                {
                    Actions = new List<CardAction>
                    {
                        new CardAction {Title = "Show me products assoicated with this opportunity", Value="Show me products assoicated with this opportunity", Type= ActionTypes.ImBack},
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


        private async Task<List<IEntity>> FetchRecentOpportunitiesForAccount(string accountName, bool onlyOpenOpportunities)
        {
            var fetchXml = @"
                  <fetch mapping=""logical"" version=""1.0"" output-format=""xml-platform"" distinct=""false"">
                  <entity name=""opportunity"">
                    <all-attributes />";
            if (onlyOpenOpportunities)
            {
                fetchXml += $@"<filter type=""and"" >
                            <condition value=""{this.OpenStateCode}"" attribute=""statecode"" operator=""eq"" />
                            </filter>";
            }
            fetchXml += $@"<order descending=""true"" attribute=""modifiedon"" />
                    <link-entity name=""account"" to=""parentaccountid"" from=""accountid"" alias=""ac"">
                      <attribute name=""name"" alias=""accountname"" />
                      <filter type=""and"">
                        <condition value=""%{accountName}%"" attribute=""{SearchOpportunityDialog.FilterAttributeName}"" operator=""like"" />
                      </filter>
                    </link-entity>
                  </entity>
                </fetch>";

            return (await this.crmClient.RetrieveRecordsAsync(SearchOpportunityDialog.EntityLogicalName, fetchXml)).ToList();
        }
    }
}