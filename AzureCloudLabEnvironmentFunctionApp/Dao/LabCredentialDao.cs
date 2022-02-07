using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureCloudLabEnvironment.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao
{
    internal class LabCredentialDao : Dao<LabCredential>
    {
        public LabCredentialDao(IConfigurationRoot config, ILogger logger) : base(config, logger)
        {
        }
    }
}
