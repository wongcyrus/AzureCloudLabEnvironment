namespace AzureCloudLabEnvironment.Model
{
    public class Lab
    {
        public string Course { get; set; }
        public string TerraformRepo { get; set; }
        public override string ToString()
        {
            return Course + "->" + TerraformRepo;
        }
    }
}
