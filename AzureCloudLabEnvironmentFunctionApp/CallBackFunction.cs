using System.IO;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Dao;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment
{
    public static class CallBackFunction
    {
        [FunctionName(nameof(CallBackFunction))]
        // ReSharper disable once UnusedMember.Global
        public static Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous,  "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("CallBackFunction function processed a request.");
            string token = req.Query["token"];
            var config = new Config(context);
            var deploymentDao = new DeploymentDao(config, log);

            var deployment = deploymentDao.Get(token);
            if(deployment==null)
                return Task.FromResult<IActionResult>(new OkObjectResult("Invalid token!"));
            if (deployment.Status == "CREATING")
            {
                Stream stream = req.Body;
                string output = new StreamReader(stream).ReadToEnd();
                deployment.Output = output;
                deployment.Status = "CREATED";
                deploymentDao.Update(deployment);

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
                log.LogInformation($"Sent CREATED Email to {deployment.Email} -> {deployment.Name} - {deployment.RepeatedTimes}");

                return Task.FromResult<IActionResult>(new OkObjectResult(output));
            }
            if (deployment.Status == "DELETING")
            {
                deployment.Status = "DELETED";
                deploymentDao.Update(deployment);
                var body = $@"
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
                log.LogInformation($"Sent DELETED Email to {deployment.Email} -> {deployment.Name} - {deployment.RepeatedTimes}");

                return Task.FromResult<IActionResult>(new OkObjectResult(deployment));
            }
            return Task.FromResult<IActionResult>(new OkObjectResult("Unknown status!"));
        }
    }
}
