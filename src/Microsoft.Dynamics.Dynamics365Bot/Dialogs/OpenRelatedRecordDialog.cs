namespace Microsoft.Dynamics.Dynamics365Bot.Dialogs
{
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Connector;
    using Microsoft.Dynamics.BotFramework;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Dialog to open related records.
    /// </summary>
    public class OpenRelatedRecordDialog : IDialog<object>
    {
        private readonly CrmClient crmClient = default;
        private readonly LuisResult luisResult = default;

        private const int NumberOfRecordsToDisplay = 5;

        /// <summary>
        /// Initialize OpenRelatedRecordDialog.
        /// </summary>
        /// <param name="crmClient">CRM client for interacting with CRM</param>
        /// <param name="luisResult">LUIS result</param>
        public OpenRelatedRecordDialog(CrmClient crmClient, LuisResult luisResult)
        {
            this.crmClient = crmClient;
            this.luisResult = luisResult;
        }

        /// <summary>
        /// Start interaction with user.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle received message. 
        /// </summary>
        /// <param name="context">Dialog context</param>
        /// <param name="result">Message sent by the user</param>
        /// <returns>Awaitable task.</returns>
        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            if (!context.PrivateConversationData.TryGetValue("LastEntityReference", out string savedEntity))
            {
                await context.PostAsync("You need to first search for a record.");
                context.Done(false);
                return;
            }

            var lastEntity = JsonConvert.DeserializeObject<EntityReference>(savedEntity);
            var luis = this.luisResult.Entities
                .Where(e =>
                    e.Type.Equals("EntityType") &&
                    !lastEntity.LogicalName.Equals(e.Resolution.Values.FirstOrDefault()));

            if (luis.Count() == 0)
            {
                await context.PostAsync("You did not specify any related entities to search for.");
                context.Done(false);
                return;
            }

            var entityName = (luis.First().Resolution.Values.First() as List<object>).FirstOrDefault() as string;

            var  relatedEntityMapping = this.GetRelatedEntityMapping(entityName, lastEntity.LogicalName);
            if (relatedEntityMapping == null)
            {
                await context.PostAsync($"Cannot search for {luis.First().Entity} in { lastEntity.LogicalName}");
                context.Done(false);
                return;
            }

            var relatedRecords = (await this.crmClient.RetrieveRecordsAsync(
                logicalName: relatedEntityMapping.LogicalName,
                filter: $"{relatedEntityMapping.AttributeName} eq { lastEntity.Id.ToString() }")).ToList();

            if (relatedRecords.Count == 0)
            {
                await context.PostAsync("Did not find any records.");
                context.Done(true);
                return;
            }

            var form = (await this.crmClient.RetrieveFormsOf(relatedEntityMapping.LogicalName, type: relatedEntityMapping.FormCode)).First();
            var recordsToDisplay = relatedRecords.Take(OpenRelatedRecordDialog.NumberOfRecordsToDisplay).ToList();
            var reply = context.MakeMessage() as Activity;

            foreach (var record in recordsToDisplay)
            {
                reply.Attachments.Add(await this.crmClient.RenderReadOnlyFormAsync(record, form));
            }

            reply.Text = $"Displaying {recordsToDisplay.Count()} records.";

            await context.PostAsync(reply);
            context.Done(true);
        }

        private RelatedEntity GetRelatedEntityMapping(string entityName, string referenceEntityName)
        {
            return relatedAttributesReference
                .Where(ar => ar.Key.Equals(referenceEntityName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(ar => ar.Value)
                .Where(ar => ar.LuisEntity.Equals(entityName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }

        //private Dictionary<string, Dictionary<string, string>> relatedAttributesReference = new Dictionary<string, Dictionary<string, string>>
        //{
        //    {
        //        "incident", new Dictionary<string, string>
        //        {
        //            { "email", "_regardingobjectid_value" },
        //            { "msdyn_holdactivity", "_regardingobjectid_value" }
        //        }
        //    },
        //    {
        //        "opportunity", new Dictionary<string, string>
        //        {
        //            { "opportunityproduct", "_opportunityid_value" }
        //        }
        //    }
        //};

        private Dictionary<string, List<RelatedEntity>> relatedAttributesReference = new Dictionary<string, List<RelatedEntity>>
        {
            {
                "incident", new List<RelatedEntity>()
                {
                    new RelatedEntity("email", "email", 11, "_regardingobjectid_value"),
                    new RelatedEntity("msdyn_holdactivity", "msdyn_holdactivity", 11, "_regardingobjectid_value")
                }
            },
            {
                "opportunity", new List<RelatedEntity>()
                {
                    new RelatedEntity("product", "opportunityproduct", 2, "_opportunityid_value"),
                }
            }
        };

        internal class RelatedEntity
        {
            public string LuisEntity { get; set; }
            public string LogicalName { get; set; }
            public int FormCode { get; set; }
            public string AttributeName { get; set; }

            public RelatedEntity()
            {
            }

            public RelatedEntity(string luisEntity, string logicalName, int formCode, string attributeName)
            {
                this.LuisEntity = luisEntity;
                this.LogicalName = logicalName;
                this.FormCode = formCode;
                this.AttributeName = attributeName;
            }
        }
    }
}