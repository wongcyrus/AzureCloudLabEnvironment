using System;
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

public static class StudentRegistrationFunction
{
    [FunctionName(nameof(StudentRegistrationFunction))]
    // ReSharper disable once UnusedMember.Global
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]
        HttpRequest req,
        ILogger log, ExecutionContext context)
    {
        log.LogInformation($"Start {nameof(StudentRegistrationFunction)}");


        if (req.Method == "GET")
        {
            if (!req.Query.ContainsKey("email") || !req.Query.ContainsKey("lab"))
            {
                return GetContentResult("Invalid Url and it should contain lab and email!");
            }

            string lab = req.Query["lab"];
            string email = req.Query["email"];
            var form = $@"
    <form id='form' method='post'>
        <input type='hidden' id='classroomName' name='lab' value='{lab}'>
        <label for='email'>Email:</label><br>
        <input type='email' id='email' name='email' size='50' value='{email}' required><br>
        <label for='subscriptionId'>Subscription ID:</label><br>
        <input type='subscriptionId' id='subscriptionId' name='subscriptionId' size='50' required><br>
        Azure Credentials<br/>
        <textarea name='credentials' required  rows='15' cols='100'></textarea>
        <br/>
        <button type='submit'>Register</button>
    </form>
   ";
            return GetContentResult(form);
        }

        if (req.Method == "POST")
        {
            log.LogInformation("POST Request");
            string lab = req.Form["lab"];
            string email = req.Form["email"];
            string subscriptionId = req.Form["subscriptionId"];
            string credentialJsonString = req.Form["credentials"];
            log.LogInformation("Student Register: " + email + " Lab:" + lab);
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(subscriptionId) ||
                string.IsNullOrWhiteSpace(credentialJsonString))
                return GetContentResult("Missing Data and Registration Failed!");
            email = email.Trim().ToLower();
            var isValidSubscriptionId = Guid.TryParse(subscriptionId, out _);
            if (!isValidSubscriptionId)
                return GetContentResult("Invalid Subscription ID format and Registration Failed!");

            var config = new Config(context);
            var subscriptionDao = new SubscriptionDao(config, log);
            var labCredentialDao = new LabCredentialDao(config, log);

            var credential = AppPrincipal.FromJson(credentialJsonString, log);

            if (string.IsNullOrWhiteSpace(lab))
            {
                lab = email.ToLower().Trim();
            }
            var subscription = new Subscription
            {
                PartitionKey = lab,
                RowKey = subscriptionId,
                Email = email
            };
            if (!subscriptionDao.IsNew(subscription))
                return GetContentResult("You can only have one Subscription Id for one lab!");

            var labCredential = new LabCredential
            {
                PartitionKey = lab,
                RowKey = email.ToLower().Trim(),
                Timestamp = DateTime.Now,
                AppId = credential.appId,
                DisplayName = credential.displayName,
                Password = credential.password,
                Tenant = credential.tenant,
                SubscriptionId = subscriptionId,
                Email = email.ToLower().Trim()
            };

            if (!await Helper.Azure.IsValidSubscriptionContributorRole(labCredential, subscriptionId))
                return GetContentResult(
                    "Your services principal is not in the contributor role for your subscription! Check your subscription ID and Services principal!");

            subscriptionDao.Add(subscription);
            labCredentialDao.Upsert(labCredential);

            return GetContentResult("Your credentials has been Registered!");
        }

        return new OkObjectResult("ok");
    }

    private static ContentResult GetContentResult(string content)
    {
        return new ContentResult
        {
            Content = GetHtml(content),
            ContentType = "text/html",
            StatusCode = 200
        };
    }

    private static string GetHtml(string content)
    {
        return $@"
<!DOCTYPE html>
<html lang='en' xmlns='http://www.w3.org/1999/xhtml'>
<head>
    <meta charset='utf-8' />
    <title>Azure Cloud Lab Environment</title>
</head>
<body>
    <h1>Azure Cloud Lab Environment</h1>
    {content}
    <footer>
        <p>Developed by <a href='https://www.vtc.edu.hk/admission/en/programme/it114115-higher-diploma-in-cloud-and-data-centre-administration/'> Higher Diploma in Cloud and Data Centre Administration Team.</a></p>
    </footer>
</body>
</html>";
    }
}