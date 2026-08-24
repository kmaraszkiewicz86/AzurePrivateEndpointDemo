namespace AzurePrivateEndpointDemo.API.Models
{
    public class ChatBotResponeDto
    {
        public string Answer { get; }

        public string[] Urls { get; }

        public ChatBotResponeDto(string answer, string[] urls)
        {
            Answer = answer;
            Urls = urls;
        }
    }
}
