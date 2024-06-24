# .NET AI Demo with Semantic Kernel

This demo was started from a repo written by [James Montemagno](https://github.com/jamesmontemagno/AIDemo/blob/master/readme.md)

My updated [demo-script](demo-script.md) walks through the stepts I took for my demo to discuss how to use Semantic Kernel to:

* Connect to OpenAI
* Chunk a text file into smaller pieces
* Setup an in memory vector store and configure the embedding service
* Add addition logging to see the network traffic
* Search the vector store
* Build a prompt to perform RAG and call Open AI


For additional references
* [Build your first AI Chat Bot with OPenAI and .NET in Minute](https://www.youtube.com/watch?v=NNPI4QQ8LhE)
* [AI for .NET Developers documentation](https://learn.microsoft.com/dotnet/ai/)
* [.NET AI Samples](https://github.com/dotnet/ai-samples)
* [RAG with Azure and OpenAI Sample](https://github.com/Azure-Samples/azure-search-openai-demo-csharp)
* [Semantic Kernel](https://learn.microsoft.com/semantic-kernel/overview/)


## Configuration

1. Create a developer account on [OpenAI](https://platform.openai.com/) and enable GPT-4 access (you may need to deposit $5)
2. Create a developer key and update **MvpWebApp/appsettings.json**
3. Open in Visual Studio or Visual Studio Code and enable multi project deployment to run both apps at the same time
4. The backend will run and a console window will be where we do our entry to call the API
