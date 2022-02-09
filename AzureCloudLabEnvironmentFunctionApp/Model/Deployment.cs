using System;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;

namespace AzureCloudLabEnvironment.Model
{
    internal class Deployment : ITableEntity
    {
        public string Email { get; set; }
        public string Name { get; set; }
        public string TerraformRepo { get; set; }
        public string Branch { get; set; }
        public int RepeatTimes { get; set; }
        public string Output { get; set; }
        public string Status { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }


        public string GetToken(string salt)
        {
            var tmpSource = salt + Name + Email + Branch + RepeatTimes;
            string ComputeSha256Hash(string rawData)
            {
                // Create a SHA256   
                using SHA256 sha256Hash = SHA256.Create();
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }

            return ComputeSha256Hash(tmpSource);
        }
    }
}
