namespace AzureCloudLabEnvironment.Model;

public class EmailMessage
{
    public string To { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
}

public class Attachment
{
    public string Content { get; set; }
    public string Name { get; set; }
    public string MediaType { get; set; }
}