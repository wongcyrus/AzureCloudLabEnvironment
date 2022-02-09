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
        public static Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("CallBackFunction function processed a request.");

            string output = req.Query["output"];
            string token = req.Query["token"];

            var config = new Config(context);
            var deploymentDao = new DeploymentDao(config, log);

            var deployment = deploymentDao.Get(token);
            if(deployment==null)
                return Task.FromResult<IActionResult>(new OkObjectResult("Invalid token!"));
            if(deployment.Status == "Created")
                return Task.FromResult<IActionResult>(new OkObjectResult("Repeated!"));

            deployment.Output = output;
            deployment.Status = "Created";
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
                Subject = $"Your lab deployment of {deployment.Name} session {deployment.RepeatTimes} is ready",
                Body = body
            };
            var emailClient = new Email(config, log);
            emailClient.Send(emailMessage, new[] { Email.StringToAttachment(output, "output.txt", "text/plain") });

            return Task.FromResult<IActionResult>(new OkObjectResult(output));
        }
    }
}
