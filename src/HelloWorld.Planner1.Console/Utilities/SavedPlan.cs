
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;

namespace HelloWorld.Planner1.Console.Utilities;

public static class HandlebarsPlannerExtensions
{
    
    public async static Task<HandlebarsPlan> CreateAndSavePlanAsync(this HandlebarsPlanner planner, string filename, Kernel kernel, string goal, KernelArguments? arguments = null)
    {
        var plan = await planner.CreatePlanAsync(kernel, goal, arguments);

        plan.Save(filename);

        return plan;
    }

    public async static Task<HandlebarsPlan> GetOrCreatePlanAsync(this HandlebarsPlanner planner, string filename, Kernel kernel, string goal, KernelArguments? arguments = null)
    {
        if (Exists(filename))
        {
            return planner.Load(filename);
        }
        else
        {
            return await planner.CreateAndSavePlanAsync(filename, kernel, goal, arguments);
        }
    }

    public static bool Exists(string filename)
    {
        return File.Exists(filename);
    }

    public static HandlebarsPlan Load(this HandlebarsPlanner planner, string filename)
    {
        // Load the saved plan
        var savedPlan = File.ReadAllText(filename);

        // Populate intance
        return new HandlebarsPlan(savedPlan);
    }

    public static void Save(this HandlebarsPlan plan, string filename)
    {
        File.WriteAllText(filename, plan.ToString());
    }
}
