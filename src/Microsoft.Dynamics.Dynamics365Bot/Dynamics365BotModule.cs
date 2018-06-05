namespace Microsoft.Dynamics.Dynamics365Bot
{
    using Autofac;
    using Autofac.Integration.WebApi;
    using BotAuth.AADv1;
    using BotAuth.Models;
    using Microsoft.Bot.Builder.Azure;
    using Microsoft.Bot.Builder.Dialogs.Internals;
    using Microsoft.Bot.Builder.Internals.Fibers;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Scorables;
    using Microsoft.Bot.Connector;
    using Microsoft.Dynamics.BotFramework;
    using Microsoft.Dynamics.Dynamics365Bot.Dialogs;
    using Microsoft.Dynamics.Dynamics365Bot.Scorables;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Reflection;
    using System.Web.Http;

    /// <summary>
    /// Autofac Module.
    /// </summary>
    public class Dynamics365BotModule : Autofac.Module
    {
        /// <summary>
        /// Override to add registrations to the container.
        /// </summary>
        /// <param name="builder">The builder through which components can be registered.</param>
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterModule<ReflectionSurrogateModule>();

            var keyVaultResourceId = ConfigHelper.Read(ConfigHelper.KeyKeyVaultResourceId);
            var azureAdInstance = ConfigHelper.Read(ConfigHelper.KeyADEndpointUrl);
            var redirectUrl = ConfigHelper.Read(ConfigHelper.KeyRedirectUrl);

            var apiVersion = ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyApiVersion).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            var organization = ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyOrganizationUrl).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            var luisModelId = ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyLuisModelId).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            var luisSubscriptionKey = ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyLuisSubscriptionKey).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            var storageConnectionString = ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyStorageAccount).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            var resourceId = ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyADResourceId).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            var tenant = ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyTenant).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            var clientId = ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyADClientId).ConfigureAwait(false).GetAwaiter().GetResult().Value;
            var clientSecret = ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyADClientSecret).ConfigureAwait(false).GetAwaiter().GetResult().Value;

            var store = new TableBotDataStore2(storageConnectionString);

            builder.RegisterModule(new CrmBotFrameworkModule(organization, apiVersion, TimeSpan.FromDays(1), 4));

            builder.RegisterModule(new AzureModule(Assembly.GetExecutingAssembly()));
            builder.Register(c => store)
                .Keyed<IBotDataStore<BotData>>(AzureModule.Key_DataStore)
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<ADALAuthProvider>()
                .AsImplementedInterfaces()
                .SingleInstance();

            var options = new AuthenticationOptions()
            {
                ResourceId = resourceId,
                Authority = $"{azureAdInstance}/{tenant}",
                RedirectUrl = redirectUrl,
                ClientId = clientId,
                ClientSecret = clientSecret
            };

            builder.Register(c => options)
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<AuthenticationScorable>()
                .As<IScorable<IActivity, double>>()
                .InstancePerLifetimeScope();

            // Cannot be made singleton as it depends upon IBotState instance for each user
            builder.RegisterType<CrmAuthenticator>()
                .Keyed<ICrmAuthenticator>(FiberModule.Key_DoNotSerialize)
                .As<ICrmAuthenticator>()
                .InstancePerLifetimeScope();

            builder.Register<Func<string, List<IEntity>>>(component =>
                {
                    return (key) => null;
                })
                .Keyed<Func<string, List<IEntity>>>(FiberModule.Key_DoNotSerialize)
                .As<Func<string, List<IEntity>>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<RootLuisDialog>()
                .WithParameter("services", new LuisService[] { new LuisService(new LuisModelAttribute(luisModelId, luisSubscriptionKey)) })
                .AsSelf()
                .InstancePerLifetimeScope();

            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());

            builder.RegisterWebApiFilterProvider(GlobalConfiguration.Configuration);
        }
    }
}