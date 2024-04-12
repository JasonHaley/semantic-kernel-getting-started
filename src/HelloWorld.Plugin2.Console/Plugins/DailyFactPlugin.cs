using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace HelloWorld.Plugin.Console.Plugins;

public class DailyFactPlugin
{
    private const string TODAY_DESCRIPTION = "Provides interesting historic facts for today's date.";

    private const string DESCRIPTION = "Provides interesting historic facts for the current date.";
    private const string TEMPLATE = @"Tell me an interesting fact from world 
        about an event that took place on {{$today}}.
        Be sure to mention the date in history for context.";
        
    private readonly KernelFunction _dailyFact;
    private readonly KernelFunction _todaysFact;

    public DailyFactPlugin()
    {
        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "Temperature", 0.7 },
                { "MaxTokens", 250 }
            }

        };
        _dailyFact = KernelFunctionFactory.CreateFromPrompt(TEMPLATE, functionName: "GetDailyFactFunc", description: DESCRIPTION, executionSettings: settings);
        _todaysFact = KernelFunctionFactory.CreateFromPrompt(TEMPLATE, functionName: "GetTodaysFactFunc", description: TODAY_DESCRIPTION, executionSettings: settings);
    }
    
    [KernelFunction, Description(DESCRIPTION)]
    public async Task<string> GetDailyFact([Description("Current date")] string today, Kernel kernel)
    {
        var result = await _dailyFact.InvokeAsync(kernel, new() { ["today"] = today }).ConfigureAwait(false);

        return result.ToString();
    }

    [KernelFunction, Description(TODAY_DESCRIPTION)]
    public async Task<string> GetTodaysDailyFact(Kernel kernel)
    {
        var today = DateTime.Now.ToString("MMMM dd");

        var result = await _todaysFact.InvokeAsync(kernel, new() { ["today"] = today }).ConfigureAwait(false);

        return result.ToString();
    }
}
