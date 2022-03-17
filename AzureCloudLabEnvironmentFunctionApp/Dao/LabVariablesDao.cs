using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzureCloudLabEnvironment.Helper;
using AzureCloudLabEnvironment.Model;
using ExcelDataReader;
using Microsoft.Extensions.Logging;

namespace AzureCloudLabEnvironment.Dao
{
    public class LabVariablesDao
    {
        private readonly Config _config;
        private readonly ILogger _logger;

        private readonly BlobServiceClient _blobServiceClient;
        private DataTable _labVariables;

        public LabVariablesDao(Config config, ILogger logger)
        {
            _config = config;
            _logger = logger;
            _blobServiceClient = new BlobServiceClient(config.GetConfig(Config.Key.AzureWebJobsStorage));
        }

        public async Task<bool> LoadVariables(Lab lab)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("lab-variables");
            var blobClientForSpecificLab = containerClient.GetBlobClient(lab.Name + "_" + lab.RepeatedTimes + "_.xlsx");
            var blobClientForEveryLab = containerClient.GetBlobClient(lab.Name + ".xlsx");
            Response<BlobDownloadResult> content;
            if (await blobClientForSpecificLab.ExistsAsync())
            {
                content = await blobClientForSpecificLab.DownloadContentAsync();
            }
            else if (await blobClientForEveryLab.ExistsAsync())
            {
                content = await blobClientForEveryLab.DownloadContentAsync();
            }
            else return false;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var excelReader = ExcelReaderFactory.CreateOpenXmlReader(content.Value.Content.ToStream());

            var result = excelReader.AsDataSet(new ExcelDataSetConfiguration()
            {
                ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                {
                    UseHeaderRow = true
                }
            });
            _labVariables = result.Tables[0];
            return true;
        }
        public Dictionary<string, string> GetVariables(string email)
        {
            if (_labVariables == null) return new Dictionary<string, string>();
            try
            {
                var row = _labVariables.Select($"Email = '{email}'").FirstOrDefault(); if (row == null) return new Dictionary<string, string>();

                var variables = row.Table.Columns
                    .Cast<DataColumn>()
                    .ToDictionary(c => c.ColumnName, c => row[c].ToString());
                var seatNumber = _labVariables.Rows.IndexOf(row);
                variables.Add("SeatNumber", seatNumber.ToString());
                return variables;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return new Dictionary<string, string>();
        }
    }
}
