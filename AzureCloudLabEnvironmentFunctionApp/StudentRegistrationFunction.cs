﻿using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Dao;
using AzureCloudLabEnvironment.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;


namespace AzureCloudLabEnvironment
{
    public static class StudentRegistrationFunction
    {
        private static AppPrincipal ReadToObject(string json)
        {
            var deserializedUser = new AppPrincipal();
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var ser = new DataContractJsonSerializer(deserializedUser.GetType());
            deserializedUser = ser.ReadObject(ms) as AppPrincipal;
            ms.Close();
            return deserializedUser;
        }

        [FunctionName(nameof(StudentRegistrationFunction))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation($"Start {nameof(StudentRegistrationFunction)}");


            if (req.Method == "GET")
            {

                if (!req.Query.ContainsKey("email") || !req.Query.ContainsKey("lab"))
                {
                    return GetContentResult("Invalid Url and it should contain lab and email!");
                }
                else
                {
                    string lab = req.Query["lab"];
                    string email = req.Query["email"];
                    string form = $@"
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
            }
            else if (req.Method == "POST")
            {
                log.LogInformation("POST Request");
                string lab = req.Form["lab"];
                string email = req.Form["email"];
                string subscriptionId = req.Form["subscriptionId"];
                string credentialJsonString = req.Form["credentials"];
                log.LogInformation("Student Register: " + email + " Lab:" + lab);
                if (string.IsNullOrWhiteSpace(lab) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(credentialJsonString))
                {
                    return GetContentResult("Missing Data and Registration Failed!");
                }
                var isValidSubscriptionId = Guid.TryParse(subscriptionId, out var guidOutput);
                if (!isValidSubscriptionId)
                {
                    return GetContentResult("Invalid Subscription ID format and Registration Failed!");
                }

                var config = Common.Config(context);
                var credential = ReadToObject(credentialJsonString);

                var subscriptionDao = new SubscriptionDao(config, log);
                var subscription = new Subscription()
                {
                    PartitionKey = lab,
                    RowKey = subscriptionId,
                    Email = email
                };
                if (!subscriptionDao.IsNew(subscription))
                {
                    return GetContentResult("You can only have one Subscription Id for one lab!");
                }
                subscriptionDao.Save(subscription);


                var labCredential = new LabCredential()
                {
                    PartitionKey = lab,
                    RowKey = email.ToLower().Trim(),
                    Timestamp = DateTime.Now,
                    AppId = credential.appId,
                    DisplayName = credential.displayName,
                    Password = credential.password,
                    Tenant = credential.tenant
                };

                var labCredentialDao = new LabCredentialDao(config, log);
                var result = labCredentialDao.Save(labCredential);


                return GetContentResult("Your credentials has been " + (result ? "Updated!" : "Registered!"));
            }

            return new OkObjectResult("ok");
        }

        private static ContentResult GetContentResult(string content)
        {
            return new ContentResult
            {
                Content = GetHtml(content),
                ContentType = "text/html",
                StatusCode = 200,
            };
        }
        private static string GetHtml(string content)
        {
            return $@"
<!DOCTYPE html>
<html lang='en' xmlns='http://www.w3.org/1999/xhtml'>
<head>
    <meta charset='utf-8' />
    <title>Azure Grader</title>
</head>
<body>
    {content}
    <footer>
        <p>Developed by <a href='https://www.vtc.edu.hk/admission/en/programme/it114115-higher-diploma-in-cloud-and-data-centre-administration/'> Higher Diploma in Cloud and Data Centre Administration Team.</a></p>
    </footer>
</body>
</html>";
        }
    }
}
