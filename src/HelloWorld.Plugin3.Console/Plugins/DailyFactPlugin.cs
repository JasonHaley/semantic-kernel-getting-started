using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HelloWorld.Plugin.Console.Plugins;

public class DailyFactPlugin
{
    private const string DESCRIPTION = "Provides interesting historic facts for the current day.";
    private const string TEMPLATE = @"Tell me an interesting fact from world about an event that took place on {{$today}}. Be sure to mention the date in history for context.";
        
    private readonly KernelFunction _dailyFact;
    private readonly KernelFunction _currentDay;
    //private readonly KernelFunction _todaysFact;

    public DailyFactPlugin()
    {
        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "Temperature", 0.3 },
                { "MaxTokens", 250 }
            }

        };
        _dailyFact = KernelFunctionFactory.CreateFromPrompt(TEMPLATE, functionName: "GetDailyFactFunc", description: DESCRIPTION, executionSettings: settings);
        _currentDay = KernelFunctionFactory.CreateFromMethod(() => DateTime.Now.ToString("MMMM dd"), "GetCurrentDay", "Retrieves the current day.");
        //_todaysFact = KernelFunctionFactory.CreateFromPrompt(TEMPLATE, functionName: "GetHistoricFactAboutToday", description: "Retrieves an interesting historic fact about today", executionSettings: settings);
    }

    //[KernelFunction, Description("Retrieves an interesting historic fact about today")]
    //public async Task<string> GetHistoricFactAboutToday(Kernel kernel)
    //{
    //    var today = DateTime.Now.ToString("MMMM dd");
    //    var result = await _todaysFact.InvokeAsync(kernel, new() { ["today"] = today }).ConfigureAwait(false);

    //    return result.ToString();
    //}

    [KernelFunction, Description(DESCRIPTION)]
    public async Task<string> GetDailyFact([Description("Current day"), Required] string today, Kernel kernel)
    {
        var result = await _dailyFact.InvokeAsync(kernel, new() { ["today"] = today }).ConfigureAwait(false);

        return result.ToString();
    }

    [KernelFunction, Description("Retrieves the current day")]
    public async Task<string> GetCurrentDay(Kernel kernel)
    {
        var today = await _currentDay.InvokeAsync(kernel);

        return today.ToString();
    }
}
