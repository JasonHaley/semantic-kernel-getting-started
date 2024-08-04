using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using System.Text;

namespace PropertyGraphRAG;
public static class Extensions
{
    
    public static async IAsyncEnumerable<StreamingKernelContent> AddStreamingMessageAsync(this ChatHistory chatHistory, IAsyncEnumerable<StreamingKernelContent> streamingMessageContents)
    {
        List<StreamingKernelContent> messageContents = [];

        StringBuilder? contentBuilder = null;
        
        await foreach (var chatMessage in streamingMessageContents.ConfigureAwait(false))
        {
            if (chatMessage.ToString() is { Length: > 0 } contentUpdate)
            {
                (contentBuilder ??= new()).Append(contentUpdate);
            }
            
            messageContents.Add(chatMessage);
            yield return chatMessage;
        }

        if (messageContents.Count != 0)
        {
            chatHistory.AddAssistantMessage(contentBuilder?.ToString() ?? string.Empty);
        }
    }
}