using System.Runtime.Serialization;

namespace AzureCloudLabEnvironment.Model
{
    [DataContract]
    public class AppPrincipal
    {
        [DataMember] public string appId;
        [DataMember] public string displayName;
        [DataMember] public string password;
        [DataMember] public string tenant;
    }
   
}