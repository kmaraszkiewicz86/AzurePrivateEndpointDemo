using AzurePrivateEndpointDemo.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzurePrivateEndpointDemo.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatbotController : ControllerBase
    {
        [HttpPost]
        public async Task<IEnumerable<ChatBotResponeDto>> AskChatbotAsync(ChatBotRequestDto chatBotRequestDto)
        {
            return null;
        }
    }
}
