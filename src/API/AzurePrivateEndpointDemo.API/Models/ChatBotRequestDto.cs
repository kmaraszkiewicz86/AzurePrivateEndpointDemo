using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AzurePrivateEndpointDemo.API.Models;

/// <summary>
/// Represents a chatbot question received from the React application.
/// </summary>
public sealed class ChatbotRequestDto
{
    /// <summary>
    /// Creates a chatbot request.
    /// </summary>
    /// <param name="question">The question entered by the user.</param>
    [JsonConstructor]
    public ChatbotRequestDto(string question)
    {
        Question = question;
    }

    [Required, StringLength(2000)]
    public string Question { get; }
}
