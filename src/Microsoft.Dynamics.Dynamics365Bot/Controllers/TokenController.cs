namespace Microsoft.Dynamics.Dynamics365Bot.Controllers
{
    using Microsoft.Bot.Connector;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    public class TokenController : ApiController
    {
        private readonly string azureAdInstance;
        private readonly string keyVaultResourceId;

        public TokenController()
        {
            this.azureAdInstance = ConfigHelper.Read(ConfigHelper.KeyADEndpointUrl);
            this.keyVaultResourceId = ConfigHelper.Read(ConfigHelper.KeyKeyVaultResourceId);
        }

        public async Task<string> Get()
        {
            return (await ConfigHelper.GetSecretFromKeyVaultAsync(azureAdInstance, keyVaultResourceId, ConfigHelper.KeyWebChatSecret)).Value;
        }
    }
}
