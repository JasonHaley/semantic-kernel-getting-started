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