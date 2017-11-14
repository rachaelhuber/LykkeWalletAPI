﻿using System;
using System.Net;
using System.Security.Principal;
using System.Threading.Tasks;
using Lykke.Service.Operations.Client;
using Lykke.Service.Operations.Client.AutorestClient;
using Lykke.Service.Operations.Contracts;
using LykkeApi2.Infrastructure;
using LykkeApi2.Models.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LykkeApi2.Controllers
{
    [Authorize]
    [Route("api/operations")]
    public class OperationsController : Controller
    {
        private readonly IOperationsClient _operationsClient;
        private readonly IRequestContext _requestContext;

        public OperationsController(IOperationsClient operationsClient, IRequestContext requestContext)
        {
            _operationsClient = operationsClient;
            _requestContext = requestContext;
        }

        /// <summary>
        /// Get operation by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{id}")]
        [ApiExplorerSettings(GroupName = "Operations")]
        public async Task<IActionResult> Get(Guid? id)
        {
            if (!id.HasValue)
                return BadRequest(new { message = "id is required" });

            var operation = await _operationsClient.Get(id.Value);

            if (operation == null)
                return NotFound();

            return Ok(operation);
        }

        /// <summary>
        /// Create transfer operation
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("transfer/{id}")]
        [ApiExplorerSettings(GroupName = "Operations")]       
        public async Task<IActionResult> Transfer([FromBody]CreateTransferRequest cmd, Guid? id)
        {
            if (!id.HasValue)
                return BadRequest(new { message = "Operation id is required" });
            
            await _operationsClient.Transfer(id.Value, 
                new CreateTransferCommand
                {
                    ClientId = new Guid(_requestContext.ClientId),
                    Amount = cmd.Amount,
                    SourceWalletId = 
                    cmd.SourceWalletId,
                    WalletId = cmd.WalletId,
                    AssetId = cmd.AssetId                    
                });
            
            return Created(Url.Action("Get", new { id }), id);
        }

        /// <summary>
        /// Cancel operation
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("cancel/{id}")]
        [ApiExplorerSettings(GroupName = "Operations")]
        public async Task<IActionResult> Cancel(Guid? id)
        {
            if (!id.HasValue)
                return BadRequest(new { message = "Operation id is required" });

            await _operationsClient.Cancel(id.Value);

            return Ok();
        }
    }
}