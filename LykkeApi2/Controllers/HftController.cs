﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Lykke.Service.HftInternalService.Client.AutorestClient;
using Lykke.Service.HftInternalService.Client.AutorestClient.Models;
using LykkeApi2.Infrastructure;
using LykkeApi2.Models.ApiKey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LykkeApi2.Controllers
{
    [Authorize]
    [Route("api/hft")]
    public class HftController : Controller
    {
        private readonly IHftInternalServiceAPI _hftInternalService;
        private readonly IRequestContext _requestContext;

        public HftController(IHftInternalServiceAPI hftInternalService, IRequestContext requestContext)
        {
            _hftInternalService = hftInternalService ?? throw new ArgumentNullException(nameof(hftInternalService));
            _requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
        }

        /// <summary>
        /// Create new api-key for existing wallet.
        /// </summary>
        /// <param name="walletId"></param>
        /// <returns></returns>
        [HttpPut("{walletId}/regenerateKey")]
        [ProducesResponseType(typeof(CreateApiKeyResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(void), (int)HttpStatusCode.NotFound)]
        [SwaggerOperation("RegenerateKey")]
        public async Task<IActionResult> RegenerateKey(string walletId)
        {
            var clientKeys = await _hftInternalService.GetKeysAsync(_requestContext.ClientId);
            var existingApiKey = clientKeys.FirstOrDefault(x => x.Wallet == walletId);
            if (existingApiKey != null)
            {
                var apiKey = await _hftInternalService.RegenerateKeyAsync(new RegenerateKeyRequest { ClientId = _requestContext.ClientId, WalletId = existingApiKey.Wallet });
                return Ok(new CreateApiKeyResponse { ApiKey = apiKey.Key, WalletId = apiKey.Wallet });
            }
            return NotFound();
        }
    }
}