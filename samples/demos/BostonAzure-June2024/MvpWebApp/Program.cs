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


	//return kernel.InvokePromptStreamingAsync<string>(prompt.ToString());
	return kernel.InvokePromptStreamingAsync<string>(question);

});

app.Run();
