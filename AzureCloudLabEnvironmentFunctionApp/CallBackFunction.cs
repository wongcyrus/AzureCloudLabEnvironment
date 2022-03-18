using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Dao;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment;

public static class CallBackFunction
{
    [FunctionName(nameof(CallBackFunction))]
    // ReSharper disable once UnusedMember.Global
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]
        HttpRequest req,
        ILogger log, ExecutionContext context)
    {
        string token = req.Query["token"];
        log.LogInformation($"CallBackFunction function processed a request. token = {token}");


        var config = new Config(context);
        var deploymentDao = new DeploymentDao(config, log);

        var deployment = deploymentDao.Get(token);
        if (deployment == null)
            return await Task.FromResult<IActionResult>(new OkObjectResult("Invalid token!"));
        if (deployment.Status == "CREATING")
        {
            var stream = req.Body;
            var output = await new StreamReader(stream).ReadToEndAsync();
            deployment.Output = output;
            deployment.Status = "CREATED";
            deploymentDao.Update(deployment);

            if (string.IsNullOrEmpty(deployment.LifeCycleHookUrl))
            {
                var body = $@"
Dear Student,

{output}

Regards,
Azure Cloud Lab Environment
";
                var emailMessage = new EmailMessage
                {
                    To = deployment.Email,
                    Subject = $"Your lab deployment of {deployment.Name} session {deployment.RepeatedTimes} is ready",
                    Body = body
                };
                var emailClient = new Email(config, log);
                emailClient.Send(emailMessage, new[] { Email.StringToAttachment(output, "output.txt", "text/plain") });
                log.LogInformation(
                    $"Sent CREATED Email to {deployment.Email} -> {deployment.Name} - {deployment.RepeatedTimes}");
            }
            else
            {
                await SendCallBack(deployment, log);
            }
            return await Task.FromResult<IActionResult>(new OkObjectResult(output));
        }

        if (deployment.Status == "DELETING")
        {
            deployment.Status = "DELETED";
            deploymentDao.Update(deployment);
            if (string.IsNullOrEmpty(deployment.LifeCycleHookUrl))
            {
                var body = @"
Dear Student,

Your lab infrastructure has been deleted!

Regards,
Azure Cloud Lab Environment
";
                var emailMessage = new EmailMessage
                {
                    To = deployment.Email,
                    Subject = $"Your lab deployment of {deployment.Name} session {deployment.RepeatedTimes} has deleted.",
                    Body = body
                };
                var emailClient = new Email(config, log);
                emailClient.Send(emailMessage, null);
                log.LogInformation(
                    $"Sent DELETED Email to {deployment.Email} -> {deployment.Name} - {deployment.RepeatedTimes}");
            }
            else
            {
                await SendCallBack(deployment, log);
            }
            return await Task.FromResult<IActionResult>(new OkObjectResult(deployment));
        }
        log.LogInformation(deployment.ToString());
        return await Task.FromResult<IActionResult>(new OkObjectResult("Unknown status!"));
    }

    private static async Task SendCallBack(Deployment deployment, ILogger log)
    {
        if (!string.IsNullOrEmpty(deployment.LifeCycleHookUrl))
        {
            using var client = new HttpClient();
            var values = new Dictionary<string, string>
            {
                {"Status", deployment.Status},
                {"Variables", deployment.Variables},
                {"Output", deployment.Output}
            };
            try
            {
                await client.PostAsync(deployment.LifeCycleHookUrl, new FormUrlEncodedContent(values));
                log.LogInformation(
                    $"Sent {deployment.Status} Callback to {deployment.LifeCycleHookUrl}");
            }
            catch (Exception ex)
            {
                log.LogError("SendCallBack Error.");
                log.LogError(ex.Message);
            }
        }
    }
}