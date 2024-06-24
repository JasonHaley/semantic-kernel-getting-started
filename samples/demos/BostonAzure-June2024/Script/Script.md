To begin, I put this code into the server app, just to first demonstrate the end-to-end flow without AI yet in the picture:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/copilot", (string question) =>
{
    return GetResponseAsync(question);
});

app.Run();

static async IAsyncEnumerable<string> GetResponseAsync(string question)
{

    foreach (string word in question.Split(' '))

    {

        await Task.Delay(250);

        yield return word + " ";

    }
}
```

That exposes a single http://localhost:5171/copilot?question=whatever endpoint where it’ll stream back to the client all the words in whatever was provided as the query string argument. By default when returning an IAsyncEnumerable<string> like this from a minimal APIs endpoint, ASP.NET will serialize as JSON, so the client just does the inverse, deserializes the streaming JSON back to an IAsyncEnumerable<string>. I typed into the client a simple sentence and demonstrated the end-to-end working, with words streaming back to the client.

 

Then I replaced the above with this:
```csharp
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKernel().AddOpenAIChatCompletion("gpt-4", builder.Configuration["AI:OpenAI:ApiKey"]);

var app = builder.Build();

app.MapGet("/copilot", (string question, Kernel kernel) =>
{

    return kernel.InvokePromptStreamingAsync<string>(question);
});

app.Run();
```

I did so manually typing in the differences, as I think it adds to the dramatic effect 😊, but you could just copy/paste.  You’ll want to change the “AI:OpenAI:ApiKey” part to the name of the environment variable storing your OpenAI API key. I then typed into the client a simple question like “What color is the sky?” and watched the response stream in, noting that this was sending the question from the client to the server to OpenAI, and then in term streaming the result back from OpenAI to the server to the client. This mirrors a typical configuration in a real app, where you would have the actual interaction with OpenAI happening from the server so that your keys aren’t exposed on the client.

I then started building on top of this, reading in a file containing a lot of code (the file this loads is in the project so that part should “just work”):

```csharp
using Microsoft.SemanticKernel;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKernel().AddOpenAIChatCompletion("gpt-4", builder.Configuration["AI:OpenAI:APIKey"]);

var app = builder.Build();

var code = File.ReadAllLines(@"TensorPrimitives.netcore.cs");

app.MapGet("/copilot", (string question, Kernel kernel) =>
{
    var prompt = new StringBuilder("Please answer this question: ").AppendLine(question);

    prompt.AppendLine("*** Code to use when answering the question: ").AppendJoin("\n", code);

    return kernel.InvokePromptStreamingAsync<string>(prompt.ToString());

});

app.Run();
```

Enter anything into the client, and it should immediately fail due to sending way too many tokens, exceeding both the throttling limits OpenAI puts in place by default as well as the context window of the model.

I then augmented this bit by bit to the final solution:

```csharp
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Text;
using System.Numerics.Tensors;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddKernel()
    .AddOpenAIChatCompletion("gpt-4", builder.Configuration["AI:OpenAI:APIKey"])
    .AddOpenAITextEmbeddingGeneration("text-embedding-3-small", builder.Configuration["AI:OpenAI:APIKey"]);

var app = builder.Build();
var tokenizer = await Tiktoken.CreateByModelNameAsync("gpt-4");
var code = File.ReadAllLines(@"TensorPrimitives.netcore.cs");
var chunks = TextChunker.SplitPlainTextParagraphs([.. code], 500, 100, null, text => tokenizer.CountTokens(text));

List<(string Content, ReadOnlyMemory<float> Vector)> db =
    chunks.Zip(await app.Services.GetRequiredService<ITextEmbeddingGenerationService>().GenerateEmbeddingsAsync(chunks)).ToList();

app.MapGet("/copilot", async (string question, Kernel kernel, ITextEmbeddingGenerationService embeddingService) =>
{
    var qe = await embeddingService.GenerateEmbeddingAsync(question);
    var prompt = new StringBuilder("Please answer this question using the provided code: ").AppendLine(question).AppendLine("*** Code:");

    int tokensRemaining = 2000;

    foreach (var c in db.OrderByDescending(c => TensorPrimitives.CosineSimilarity<float>(qe.Span, c.Vector.Span)))
    {
        if ((tokensRemaining -= tokenizer.CountTokens(c.Content)) < 0) break;
        prompt.AppendLine(c.Content);

    }

    return kernel.InvokePromptStreamingAsync<string>(prompt.ToString());
});

app.Run();
```
 
With that, I asked a question like “How is the CosineSimilarity operator implemented?”



```csharp
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Text;
using System.Numerics.Tensors;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// 4
builder.Services
    .AddKernel()
    .AddOpenAIChatCompletion("gpt-4", builder.Configuration["AI:OpenAI:APIKey"])
    .AddOpenAITextEmbeddingGeneration("text-embedding-3-small", builder.Configuration["AI:OpenAI:APIKey"]);

var app = builder.Build();
// 2: Specifying tokenizer for chunking appropriately for GPT-4
var tokenizer = await Tiktoken.CreateByModelNameAsync("gpt-4");


// 1: Chunks of no more than 500 tokens & overlap chunks by 100 tokens. Token == Text + # associated with it
// https://platform.openai.com/tokenizer
var code = File.ReadAllLines(@"TensorPrimitives.netcore.cs");
var chunks = TextChunker.SplitPlainTextParagraphs([.. code], 500, 100, null, text => tokenizer.CountTokens(text));


// 3: Take chunks and create an in-memory vector database of embeddings for querying relevance 
List<(string Content, ReadOnlyMemory<float> Vector)> db =
    chunks.Zip(await app.Services.GetRequiredService<ITextEmbeddingGenerationService>().GenerateEmbeddingsAsync(chunks)).ToList();




app.MapGet("/copilot", async (string question, Kernel kernel, ITextEmbeddingGenerationService embeddingService) =>
{
    var qe = await embeddingService.GenerateEmbeddingAsync(question);
    var prompt = new StringBuilder("Please answer this question using the provided code: ")
                    .AppendLine(question)
                    .AppendLine("*** Code:");

    int tokensRemaining = 2000;

    // Find and append most relevant based on semantic meaning up to 2000 tokens to scope the context window
    foreach (var c in db.OrderByDescending(c => TensorPrimitives.CosineSimilarity<float>(qe.Span, c.Vector.Span)))
    {
        if ((tokensRemaining -= tokenizer.CountTokens(c.Content)) < 0) 
            break;

        prompt.AppendLine(c.Content);

    }

    return kernel.InvokePromptStreamingAsync<string>(prompt.ToString());
});

app.Run();
```