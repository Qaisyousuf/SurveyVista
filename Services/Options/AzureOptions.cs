// Services/Options/AzureOptions.cs
namespace Services.Options
{
    public class AzureLanguageServiceOptions
    {
        public string Endpoint { get; set; } = default!;
        public string Key { get; set; } = default!;
        public string Region { get; set; } = default!;
    }

    public class AzureOpenAIOptions
    {
        public string Endpoint { get; set; } = default!;
        public string Key { get; set; } = default!;
        public string DeploymentName { get; set; } = default!;
    }
}
