using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HelloWorld.Plugin.Console.Plugins;

public class BlogInfoPlugin
{
    private const string DESCRIPTION = "Provides answers to blog queries.";
    //private const string TEMPLATE = @"Tell me an interesting fact from world 
    //    about an event that took place on {{$today}}.
    //    Be sure to mention the date in history for context.";
    private const string GET_DAILY_FACT_FUNC = "GetBlogInformation";
    internal const string PLUGIN_NAME = "DailyFactPlugin";
    internal const string GET_DAILY_FACT = "GetDailyFact";

    //private readonly KernelFunction _dailyFact;
    private readonly KernelFunction _currentDay;
    
    public BlogInfoPlugin()
    {
        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "Temperature", 0.0 },
                { "MaxTokens", 250 }
            }

        };
        
        //_dailyFact = KernelFunctionFactory.CreateFromPrompt(TEMPLATE,
        //    functionName: GET_DAILY_FACT_FUNC,
        //    executionSettings: settings);
        
        _currentDay = KernelFunctionFactory.CreateFromMethod(() => DateTime.Now.ToString("MMMM dd"), "GetCurrentDay");
    }
    
    [KernelFunction, Description(DESCRIPTION)]
    public async Task<string> GetBlogInformation([Description("Blog query"), Required] string query, Kernel kernel)
    {
        //var result = await _dailyFact.InvokeAsync(kernel, new() { ["today"] = today }).ConfigureAwait(false);

        return "Hello World";
    }

    [KernelFunction, Description("Retrieves the current day.")]
    public async Task<string> GetCurrentDay(Kernel kernel)
    {
        var today = await _currentDay.InvokeAsync(kernel);

        return today.ToString();
    }
}
