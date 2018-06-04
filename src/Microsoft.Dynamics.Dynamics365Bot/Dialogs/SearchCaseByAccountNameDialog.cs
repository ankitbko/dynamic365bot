namespace Microsoft.Dynamics.Dynamics365Bot.Dialogs
{
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using Microsoft.Dynamics.BotFramework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Search case by an account name provided by the user and render an appropriate response.
    /// </summary>
    public class SearchCaseByAccountNameDialog: IDialog<object>
    {
        private string query = default;
        private const string EntityLogicalName = "incident";
        private const string FilterAttributeName = "name";
        private CrmClient crmClient = default;
        private int previousDays = 0;
        private readonly ICache<string, List<IEntity>> entityCache;
        private const string EntityCacheSuffix = nameof(SearchCaseByAccountNameDialog);
        private readonly Dictionary<string, string> filterOptions = new Dictionary<string, string>() { { "Case Type", "_msdyn_casetypeid_value@OData.Community.Display.V1.FormattedValue" }, { "Account Name", "accountname" } };
        private string selectedFilterOption = string.Empty;
        private const string showAllOption = "Show me everything";

        /// <summary>
        /// Initialize SearchCaseByAccountNameDialog.
        /// </summary>
        /// <param name="crmClient">CRM client for interacting with CRM</param>
        /// <param name="entityCache">List of entities in the cache</param>
        /// <param name="query">Query</param>
        /// <param name="days">Days</param>
        public SearchCaseByAccountNameDialog(CrmClient crmClient, ICache<string, List<IEntity>> entityCache, string query, int days = 3)
        {
            this.query = query;
            this.crmClient = crmClient;
            this.previousDays = days;
            this.entityCache = entityCache;
        }

        /// <summary>
        /// Start interaction with the user.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <returns>Awaitable task.</returns>
        public async Task StartAsync(IDialogContext context)
        {
            if (string.IsNullOrEmpty(this.query))
            {
                await context.PostAsync("Enter account name");
                context.Wait(this.AccountNameReceivedAsync);
            }
            else
            {
                await SearchAsync(context, this.query);
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
            this.query = message.Text;
            await this.SearchAsync(context, this.query);
        }

        /// <summary>
        /// Search incidents using the query parameter.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="query">Query</param>
        /// <returns>Awaitable task.</returns>
        private async Task SearchAsync(IDialogContext context, string query)
        {
            var incidents = await this.FetchRecentIncidents(query);
            if (incidents.Count == 0)
            {
                await context.PostAsync($"No recent cases for {query} was submitted in last {this.previousDays} days.");
                context.Done(false);
            }
            else if (incidents.Count > 5)
            {
                this.entityCache.Add($"{context.Activity.From.Id}{SearchCaseByAccountNameDialog.EntityCacheSuffix}", incidents);
                await context.PostAsync($"I found {incidents.Count} deals submitted for {query} in last {this.previousDays} days. You can further filter using below filter options.");

                var caseTypes = incidents.Select(incident => incident.GetFieldValue("_msdyn_casetypeid_value@OData.Community.Display.V1.FormattedValue", string.Empty));
                var filterOptions = this.filterOptions.Keys.ToList();
                filterOptions.Add(SearchCaseByAccountNameDialog.showAllOption);
                var options = new PromptOptions<string>(
                    "How would you like to filter?",
                    "Incorrect value. Please select one of the opions below",
                    "You exceeded number of attempts. Please try asking your question again.",
                    filterOptions);
                PromptDialog.Choice(context, this.ResumeAfterFilterSelectionAsync, options);
            }
            else
            {
                await this.DisplayIncidentsAsync(context, incidents, $"I found {incidents.Count} deals for {query} submitted in last {this.previousDays} days");
                context.Done(true);
            }
        }

        /// <summary>
        /// Resume after filter selection.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="result">Message sent by the user</param>
        /// <returns>Awaitable task.</returns>
        private async Task ResumeAfterFilterSelectionAsync(IDialogContext context, IAwaitable<string> result)
        {
            this.selectedFilterOption = await result;

            var incidents = this.entityCache[($"{context.Activity.From.Id}{SearchCaseByAccountNameDialog.EntityCacheSuffix}")];
            if (incidents == null)
            {
                incidents = await this.FetchRecentIncidents(this.query);
            }

            if (selectedFilterOption.Equals(SearchCaseByAccountNameDialog.showAllOption))
            {
                await this.DisplayIncidentsAsync(
                context,
                incidents: incidents,
                text: $"Displaying {incidents.Count} cases submitted in last {this.previousDays} days");

                this.entityCache.Remove($"{context.Activity.From.Id}{SearchCaseByAccountNameDialog.EntityCacheSuffix}");
                context.Done(true);
            }
            else
            {
                var uniqueOptions = incidents.Select(incident => incident.GetFieldValue(this.filterOptions[this.selectedFilterOption], string.Empty)).Distinct().ToList();

                var options = new PromptOptions<string>(
                    $"Please select {this.selectedFilterOption} from below.",
                    options: uniqueOptions);

                PromptDialog.Choice(context, this.ResumeAfterFilterValueAsync, options);
            }
        }

        /// <summary>
        /// Resume after filtering a value.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="result">Message sent by the user</param>
        /// <returns>Awaitable task.</returns>
        private async Task ResumeAfterFilterValueAsync(IDialogContext context, IAwaitable<string> result)
        {
            var filterValue = await result;

            var incidents = this.entityCache[$"{context.Activity.From.Id}{SearchCaseByAccountNameDialog.EntityCacheSuffix}"];
            if (incidents == null)
            {
                incidents = await this.FetchRecentIncidents(this.query);
            }

            var filteredIncidents = incidents.Where(incident => incident.GetFieldValue<string>(this.filterOptions[this.selectedFilterOption]).Equals(filterValue)).ToList();

            await this.DisplayIncidentsAsync(
                context,
                incidents: filteredIncidents,
                text: $"Displaying {filteredIncidents.Count} cases with {this.selectedFilterOption} as {filterValue} submitted in last {this.previousDays} days.");

            this.entityCache.Remove($"{context.Activity.From.Id}{SearchCaseByAccountNameDialog.EntityCacheSuffix}");
            context.Done(true);
        }

        /// <summary>
        /// Display incidents.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="incidents">List of incidents</param>
        /// <param name="text">Reply text</param>
        /// <returns>Awaitable task.</returns>
        private async Task DisplayIncidentsAsync(IDialogContext context, List<IEntity> incidents, string text)
        {

            var form = await this.crmClient.RetrieveFormsOf(incidents.First().LogicalName, 11);
            
            if (form.Count() == 0)
            {
                await context.PostAsync("No form found to display result.");
                return;
            }

            int chunkSize = 10;
            var chunks = incidents.Chunks(chunkSize);

            var reply = context.MakeMessage();
            reply.Text = text;

            foreach (var chunk in chunks)
            {
                foreach (var record in chunk)
                {
                    reply.Attachments.Add(await this.crmClient.RenderReadOnlyFormAsync(record, form.FirstOrDefault()));
                }
                await context.PostAsync(reply);
                reply = context.MakeMessage();
            }
        }

        /// <summary>
        /// Resume after account selection.
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="account">Account entity</param>
        /// <returns>Awaitable task</returns>
        private async Task ResumeAfterAccountSelectionAsync(IDialogContext context, IEntity account)
        {
            try
            {
                var incidents = await this.RetrieveIncidentsFromAccount(account);
                if (incidents.Count == 0)
                {
                    await context.PostAsync($"No active cases for account {account.GetFieldValue("name", string.Empty)}");
                    context.Done(false);
                    return;
                }

                var form = (await this.crmClient.RetrieveFormsOf(incidents.First().LogicalName, 11)).First();

                var reply = context.MakeMessage();
                foreach (var record in incidents)
                {
                    reply.Attachments.Add(await this.crmClient.RenderReadOnlyFormAsync(record, form));
                }
                reply.Text = $"Found {incidents.Count} active cases";
                await context.PostAsync(reply);

                context.Done(true);
            }
            catch (Exception exception)
            {
                context.Fail(exception);
            }

            context.Done(true);
        }

        /// <summary>
        /// Retrieve list of incident entities from account entity.
        /// </summary>
        /// <param name="account">Account entity</param>
        /// <returns>List of incident entities as an awaitable task.</returns>
        private async Task<List<IEntity>> RetrieveIncidentsFromAccount(IEntity account)
        {
            var nextLink = account.GetFieldValue("incident_customer_accounts@odata.nextLink", string.Empty);
            return (await this.crmClient.RetrieveRecordsUsingAbsoluteUrlAsync("incident", nextLink)).ToList();
        }

        /// <summary>
        /// Fetch recent incidents.
        /// </summary>
        /// <param name="query">Query</param>
        /// <returns>List of incident entities as an awaitable task.</returns>
        private async Task<List<IEntity>> FetchRecentIncidents(string query)
        {
            var fetchXml = $@"
                  <fetch mapping=""logical"" version=""1.0"" output-format=""xml-platform"" distinct=""false"">
                  <entity name=""incident"">
                    <all-attributes />
                    <order descending=""true"" attribute=""modifiedon"" />
                    <filter type=""and"">
                      <condition value=""{this.previousDays}"" attribute=""modifiedon"" operator=""last-x-days"" />
                    </filter>
                    <link-entity name=""account"" to=""customerid"" from=""accountid"" alias=""ac"">
                      <attribute name=""name"" alias=""accountname"" />
                      <filter type=""and"">
                        <condition value=""%{query}%"" attribute=""{SearchCaseByAccountNameDialog.FilterAttributeName}"" operator=""like"" />
                      </filter>
                    </link-entity>
                  </entity>
                </fetch>";

            return (await this.crmClient.RetrieveRecordsAsync(SearchCaseByAccountNameDialog.EntityLogicalName, fetchXml)).ToList();
        }
    }
}