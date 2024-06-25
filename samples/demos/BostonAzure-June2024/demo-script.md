NOTE: This is modified from [this script created by James Montemagno](https://github.com/jamesmontemagno/AIDemo/blob/master/Script/Script.md)

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

That exposes a single http://localhost:5171/copilot?question=**[question typed in console goe here]**

Whatever endpoint where it’ll stream back to the client all the words in whatever was provided as the query string argument. By default when returning an IAsyncEnumerable<string> like this from a minimal APIs endpoint, ASP.NET will serialize as JSON, so the client just does the inverse, deserializes the streaming JSON back to an IAsyncEnumerable<string>. I typed into the client a simple sentence and demonstrated the end-to-end working, with words streaming back to the client.

 
Then I these lines and remove the `GetREsponseAsync` method
```csharp

// Step 1: get connection to LLM working and round trip


// Add above builder.Build();
builder.Services.AddKernel()
   .AddOpenAIChatCompletion("gpt-4o", builder.Configuration["AI:OpenAI:ApiKey"]); 

// add to parameters in MapGet
Kernel kernel 

// add to body of MapGet
return kernel.InvokePromptStreamingAsync<string>(question);

```

I did so manually typing in the differences, as I think it adds to the dramatic effect ??, but you could just copy/paste.  You’ll want to change the “AI:OpenAI:ApiKey” part to the name of the environment variable storing your OpenAI API key. I then typed into the client a simple question like “What color is the sky?” and watched the response stream in, noting that this was sending the question from the client to the server to OpenAI, and then in term streaming the result back from OpenAI to the server to the client. This mirrors a typical configuration in a real app, where you would have the actual interaction with OpenAI happening from the server so that your keys aren’t exposed on the client.

I then started building on top of this, reading in a file containing a lot of code (the file this loads is in the project so that part should “just work”):


```csharp
// Step 2: Text Chunking

// Add below the app.Build()

var code = File.ReadAllLines(@"transcript.txt"); 

var tokenizer = Tiktoken.CreateTiktokenForModel("gpt-4o");
var chunks = TextChunker.SplitPlainTextParagraphs([.. code], 500, 100, null, text => tokenizer.CountTokens(text));

```

Discuss the points of what the text chunking is all about.

```csharp
// Step 3 Vector Store

// Add below the text chunking
var embeddingService = app.Services.GetRequiredService<ITextEmbeddingGenerationService>();

var memoryBuilder = new MemoryBuilder();
memoryBuilder.WithTextEmbeddingGeneration(embeddingService);
memoryBuilder.WithMemoryStore(new VolatileMemoryStore());

var memory = memoryBuilder.Build();

for (int i = 0; i < 10; i++) // TODO: Change later if want all 70+ chunks embedded
{
	await memory.SaveInformationAsync("chunks", id: i.ToString(), text: chunks[i]);
}


// Add logging
builder.Services.AddKernel()
	.AddOpenAIChatCompletion("gpt-4o", builder.Configuration["AI:OpenAI:ApiKey"], null, null, new HttpClient(new RequestAndResponseLoggingHttpClientHandler()))
	.AddOpenAITextEmbeddingGeneration("text-embedding-ada-002", builder.Configuration["AI:OpenAI:ApiKey"], null, null, new HttpClient(new RequestLoggingHttpClientHandler()));


```


```csharp
// Step 4:  Search the Vector Store

// Add to body of MapGet
var results = await memory.SearchAsync("chunks", question, 10, 0.6).ToListAsync();

int tokensRemaining = 2000;
foreach( var result in results)
	{
		//-----------------------------------------------------------------------------------------------------------------------------
		// Keep Prompt under specific size
		if ((tokensRemaining -= tokenizer.CountTokens(result.Metadata.Text)) < 0)
			break;
		//-----------------------------------------------------------------------------------------------------------------------------

		System.Console.WriteLine($"Search Result: {result.Relevance.ToString("P")}");
		System.Console.WriteLine(result.Metadata.Text);
		System.Console.WriteLine("");
	}

```

```csharp
// Step 5: Build the prompt and call LLM

	// Add before the foreach loop
	var prompt = new StringBuilder("Please answer the question with only the context provided.")
		.AppendLine("*** Quesiton: ")
		.AppendLine(question)
		.AppendLine("*** Context: ");

		// Add in the foreach loop
		prompt.AppendLine(result.Metadata.Text);

	return kernel.InvokePromptStreamingAsync<string>(prompt.ToString());
```


```csharp
	
	//return kernel.InvokePromptStreamingAsync<string>(prompt.ToString());
	return kernel.InvokePromptStreamingAsync<string>(question);
```

**Questions to Ask:**
* When did Pamela Fox present at Boston Azure?
* What did Pamela Fox present at Boston Azure?
* Can you summarize what Pamela Fox presented at Boston Azure?


Final solution if needed for reference/troubleshooting:

```csharp
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddKernel()
//	.AddOpenAIChatCompletion("gpt-4o", builder.Configuration["AI:OpenAI:ApiKey"])
//	.AddOpenAITextEmbeddingGeneration("text-embedding-ada-002", builder.Configuration["AI:OpenAI:ApiKey"]);

builder.Services.AddKernel()
	.AddOpenAIChatCompletion("gpt-4o", builder.Configuration["AI:OpenAI:ApiKey"], null, null, new HttpClient(new RequestAndResponseLoggingHttpClientHandler()))
	.AddOpenAITextEmbeddingGeneration("text-embedding-ada-002", builder.Configuration["AI:OpenAI:ApiKey"], null, null, new HttpClient(new RequestLoggingHttpClientHandler()));

var app = builder.Build();

// Step 2: Text Chunking
var code = File.ReadAllLines(@"transcript.txt");
var tokenizer = Tiktoken.CreateTiktokenForModel("gpt-4o");
var chunks = TextChunker.SplitPlainTextParagraphs(code, 500, 100, null, text => tokenizer.CountTokens(text));

// Step 3: Vector Store
var embeddingService = app.Services.GetRequiredService<ITextEmbeddingGenerationService>();

var memoryBuilder = new MemoryBuilder();
memoryBuilder.WithTextEmbeddingGeneration(embeddingService);
memoryBuilder.WithMemoryStore(new VolatileMemoryStore());

var memory = memoryBuilder.Build();

for (int i = 0; i < 10; i++)
{
	await memory.SaveInformationAsync("chunks", id: i.ToString(), text: chunks[i]);
}

app.MapGet("/copilot", async (string question, Kernel kernel) =>
{
	// Step 4:  Search the Vector Store
	var results = await memory.SearchAsync("chunks", question, 10, 0.6).ToListAsync();

	// Step 5: Build the prompt and call LLM
	var prompt = new StringBuilder("Please answer the question with only the context provided.")
		.AppendLine("*** Quesiton: ")
		.AppendLine(question)
		.AppendLine("*** Context: ");


	int tokensRemaining = 2000;
	foreach (var result in results)
	{
		//-----------------------------------------------------------------------------------------------------------------------------
		// Keep Prompt under specific size
		if ((tokensRemaining -= tokenizer.CountTokens(result.Metadata.Text)) < 0)
			break;
		//-----------------------------------------------------------------------------------------------------------------------------

		System.Console.WriteLine($"Search Result: {result.Relevance.ToString("P")}");
		System.Console.WriteLine(result.Metadata.Text);
		System.Console.WriteLine("");

		prompt.AppendLine(result.Metadata.Text);
	}


	return kernel.InvokePromptStreamingAsync<string>(prompt.ToString());
	//return kernel.InvokePromptStreamingAsync<string>(question);

});

app.Run();

```

Bonus round if have time
```csharp

// Remove previous pompt building and use the prompt files in the project

// Change the string builder to more descriptive name
var context = new StringBuilder();

// update foreach to use 
context.AppendLine(result.Metadata.Text);

// replace the kernel invoke with a prompt invoke

var prompts = kernel.CreatePluginFromPromptDirectory("Prompts");
	return prompts["RAG"].InvokeStreamingAsync<string>(kernel, new KernelArguments()
		{
			{ "question", question },
			{ "context", context.ToString() }
		});

```

Final Final code

```csharp
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using System.Collections.Frozen;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddKernel()
//	.AddOpenAIChatCompletion("gpt-4o", builder.Configuration["AI:OpenAI:ApiKey"])
//	.AddOpenAITextEmbeddingGeneration("text-embedding-ada-002", builder.Configuration["AI:OpenAI:ApiKey"]);

builder.Services.AddKernel()
	.AddOpenAIChatCompletion("gpt-4o", builder.Configuration["AI:OpenAI:ApiKey"], null, null, new HttpClient(new RequestAndResponseLoggingHttpClientHandler()))
	.AddOpenAITextEmbeddingGeneration("text-embedding-ada-002", builder.Configuration["AI:OpenAI:ApiKey"], null, null, new HttpClient(new RequestLoggingHttpClientHandler()));

var app = builder.Build();

// Step 2: Text Chunking
var code = File.ReadAllLines(@"transcript.txt");
var tokenizer = Tiktoken.CreateTiktokenForModel("gpt-4o");
var chunks = TextChunker.SplitPlainTextParagraphs(code, 500, 100, null, text => tokenizer.CountTokens(text));

// Step 3: Vector Store
var embeddingService = app.Services.GetRequiredService<ITextEmbeddingGenerationService>();

var memoryBuilder = new MemoryBuilder();
memoryBuilder.WithTextEmbeddingGeneration(embeddingService);
memoryBuilder.WithMemoryStore(new VolatileMemoryStore());

var memory = memoryBuilder.Build();

for (int i = 0; i < 10; i++)
{
	await memory.SaveInformationAsync("chunks", id: i.ToString(), text: chunks[i]);
}

app.MapGet("/copilot", async (string question, Kernel kernel) =>
{
	// Step 4:  Search the Vector Store
	var results = await memory.SearchAsync("chunks", question, 10, 0.6).ToListAsync();

	var context = new StringBuilder();

	int tokensRemaining = 2000;
	foreach (var result in results)
	{
		//-----------------------------------------------------------------------------------------------------------------------------
		// Keep Prompt under specific size
		if ((tokensRemaining -= tokenizer.CountTokens(result.Metadata.Text)) < 0)
			break;
		//-----------------------------------------------------------------------------------------------------------------------------

		System.Console.WriteLine($"Search Result: {result.Relevance.ToString("P")}");
		System.Console.WriteLine(result.Metadata.Text);
		System.Console.WriteLine("");

		//	prompt.AppendLine(result.Metadata.Text);
		context.AppendLine(result.Metadata.Text);
	}

	
	var prompts = kernel.CreatePluginFromPromptDirectory("Prompts");
	return prompts["RAG"].InvokeStreamingAsync<string>(kernel, new KernelArguments()
		{
			{ "question", question },
			{ "context", context.ToString() }
		});

});

app.Run();
```