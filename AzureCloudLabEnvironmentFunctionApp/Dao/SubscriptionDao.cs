using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureCloudLabEnvironment.Dao
{
    internal class SubscriptionDao :Dao
    {
        public SubscriptionDao(IConfigurationRoot config, ILogger logger) : base(config, logger)
        {
        }
        //public async  Subscription GetSubscription(string lab, string subscriptionId)
        //{
        //    var retrieveOperation = TableOperation.Retrieve<Subscription>(lab, subscriptionId);
        //    return await ExecuteTableOperation(retrieveOperation) as Subscription;

          
        //}


    }
}
