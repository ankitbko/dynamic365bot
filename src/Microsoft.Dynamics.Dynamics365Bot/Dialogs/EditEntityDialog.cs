namespace Microsoft.Dynamics.Dynamics365Bot.Dialogs
{
    using Microsoft.Bot.Builder.Dialogs;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;
    using System.Threading.Tasks;
    using Microsoft.Dynamics.BotFramework;
    using Microsoft.Bot.Builder.Luis.Models;
    using Newtonsoft.Json;
    using Microsoft.Bot.Connector;
    using Newtonsoft.Json.Linq;

    public class EditEntityDialog : IDialog<object>
    {
        private readonly CrmClient crmClient = default;
        private readonly LuisResult luisResult = default;
        private EditData editData = default;

        public EditEntityDialog(CrmClient crmClient, LuisResult luisResult)
        {
            this.crmClient = crmClient;
            this.luisResult = luisResult;
        }
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            if (!context.PrivateConversationData.TryGetValue("LastEntityReference", out string savedEntity))
            {
                await context.PostAsync("You need to first search for a record.");
                context.Done(false);
                return;
            }
            this.editData = new EditData();
            this.editData.LastEntity = JsonConvert.DeserializeObject<EntityReference>(savedEntity);

            await context.PostAsync("Just a moment...");
            var recordTask = this.crmClient.RetrieveRecordAsync(this.editData.LastEntity);
            var formTask = this.crmClient.RetrieveFormsOf(this.editData.LastEntity.LogicalName, 11);

            var record = await recordTask;
            if (record.GetFieldValue("statecode", -1) != 0)
            {
                await context.PostAsync("This case is not active. You need to activate the case before you can edit.");
                await context.PostAsync("Case reactivation feature is coming up. For now, please reactivate the case manually in CRM.");
                context.Done(false);
                return;
            }

            var form = (await formTask)
                .Where(singleForm => {
                    var formName = singleForm.GetFieldValue<string>("name");
                    return (formName.IndexOf("bot", StringComparison.OrdinalIgnoreCase) >= 0)
                        && (formName.IndexOf("edit", StringComparison.OrdinalIgnoreCase) >= 0);
                    })
                .FirstOrDefault();

            if(form == null)
            {
                await context.PostAsync("No Edit form found.");
                context.Done(false);
                return;
            }

            var reply = context.MakeMessage();
            reply.Attachments.Add(await this.crmClient.RenderEditFormAsync(record, form));
            await context.PostAsync(reply);

            this.editData.FieldNames = form.Sections.SelectMany(section => section.Fields.Select(field => field.FieldName)).ToList();
            context.Wait(ResumeAfterSubmit);
        }

        private async Task ResumeAfterSubmit(IDialogContext context, IAwaitable<IMessageActivity> message)
        {
            try
            {
                var submitValue = (await message).Value as JObject;

                if (submitValue.Value<string>("type").Equals("cancel", StringComparison.OrdinalIgnoreCase))
                {
                    await context.PostAsync("Form is cancelled. What would you like to do next?");
                    context.Done(false);
                    return;
                }

                var date = submitValue.Value<string>("msdyn_receiveddate");
                //submitValue["msdyn_receiveddate"] = DateTime.Parse(date).ToString("yyyy-MM-ddThh:mm:ssZ");
                submitValue.Remove("type");
                var patchResult = await this.crmClient.PatchRecordAsync(this.editData.LastEntity.LogicalName, JsonConvert.SerializeObject(submitValue), this.editData.LastEntity.Id);
                await context.PostAsync("Record is updated. Whats next?");
                context.Done(true);
            }
            catch
            {
                await context.PostAsync("Something went horribly wrong while updating the record. Please update the record in CRM directly.");
                context.Done(false);
            }
        }

        private class EditData
        {
            public IEntityReference LastEntity { get; set; }
            public List<string> FieldNames { get; set; }
        }
    }
}