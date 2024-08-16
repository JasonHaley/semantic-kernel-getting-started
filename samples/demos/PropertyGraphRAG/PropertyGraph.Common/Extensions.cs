using Microsoft.SemanticKernel;
using System.Text.Json;

namespace PropertyGraph.Common;

public static class StringExtensions
{
    public static string CleanJsonText(this string jsonText)
    {
        return jsonText.Replace("```json", "").Replace("```", "").Replace("'", "").Trim();
    }
}

public static class FunctionResultExtentions
{
    public static string ToCleanString(this FunctionResult? result)
    {
        if (result == null)
        {
            return "";
        }

        string jsonText = result.ToString();
        return jsonText.CleanJsonText();
    }

    public static T? As<T>(this FunctionResult? result)
    {
        if (result == null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(result.ToCleanString());
    }
}

public static class KernelExtensions
{
    public static async Task<T?> InvokePromptAsync<T>(this Kernel kernel, KernelFunction plugin, KernelArguments kernelArguments) 
    {
        var result = await kernel.InvokeAsync(plugin, kernelArguments);

        return result.As<T>();
    }
}