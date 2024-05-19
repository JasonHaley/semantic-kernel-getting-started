
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;

namespace HelloWorld.Planner1.Console.Utilities;
internal static class SavedPlan
{
    public static string FileName = "SavedPlan.hbs";

    public static bool Exists
    {
        get
        {
            return File.Exists(FileName);
        }
    }

    public static HandlebarsPlan LoadFromFile()
    {
        // Load the saved plan
        var savedPlan = File.ReadAllText(FileName);

        // Populate intance
        return new HandlebarsPlan(savedPlan);
    }

    public static void SaveToFile(HandlebarsPlan plan)
    {
        File.WriteAllText(FileName, plan.ToString());
    }
}

public static class HandlebarsPlannerExtensions
{
    public async static Task<HandlebarsPlan> CreateAndSavePlanAsync(this HandlebarsPlanner planner, Kernel kernel, string goal, KernelArguments? arguments = null)
    {
        var plan = await planner.CreatePlanAsync(kernel, goal);

        SavedPlan.SaveToFile(plan);

        return plan;
    }
}
