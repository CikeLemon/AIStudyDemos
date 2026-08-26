using AI.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.Agents.AI.Workflows;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(AICommon).Assembly)
    .Build();

string baseUrl = configuration.GetSection("AIEndpoint").Value!;
string apiKey = configuration.GetSection("AIApiKey").Value!;
const string model = "glm-5.1";

Func<string, string> uppercaseFunc = s => s.ToLower();
var uppercase = uppercaseFunc.BindAsExecutor("UppercaseExecutor");
ReverseTextExecutor reverse = new();
WorkflowBuilder builder = new(uppercase);
builder.AddEdge(uppercase, reverse).WithOutputFrom(reverse);
var workflow = builder.Build();

await using Run run = await InProcessExecution.RunAsync(workflow, "Hello, World!");
foreach (WorkflowEvent evt in run.NewEvents)
{
    if (evt is ExecutorCompletedEvent executorComplete)
    {
        Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
    }
}

// Step 2: Reverse the string and yield output
class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(string.Concat(message.Reverse()));
    }
}