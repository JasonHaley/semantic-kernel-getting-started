# .NET AI Demo with Semantic Kernel

This demo was code was showing in my video [Build your first AI Chat Bot with OPenAI and .NET in Minute](https://www.youtube.com/watch?v=NNPI4QQ8LhE). This demo was created by the wonderful Stephen Toub and was used with his permission to remix it into videos and sample code for user group presentations.

A full [Script](./Script/Script.md) is available that walks through that demo and a deeper demo showing Tokenization, Vector Databases, and deeper integration with Semantic Kernel.

For deeper dives into .NET and AI see:
* [AI for .NET Developers documentation](https://learn.microsoft.com/dotnet/ai/)
* [.NET AI Samples](https://github.com/dotnet/ai-samples)
* [RAG with Azure and OpenAI Sample](https://github.com/Azure-Samples/azure-search-openai-demo-csharp)
* [Semantic Kernel](https://learn.microsoft.com/semantic-kernel/overview/)


## Configuration

1. Create a developer account on [OpenAI](https://platform.openai.com/) and enable GPT-4 access (you may need to deposit $5)
2. Create a developer key and update **MvpWebApp/appsettings.json**
3. Open in Visual Studio or Visual Studio Code and enable multi project deployment to run both apps at the same time
4. The backend will run and a console window will be where we do our entry to call the API
