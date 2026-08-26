
using AI.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(AICommon).Assembly)
    .Build();

string baseUrl = configuration.GetSection("AIEndpoint").Value!;
string apiKey = configuration.GetSection("AIApiKey").Value!;
const string model = "glm-5.1";

HttpClientInterceptor.StartInterception();

var jokerAgent = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(model)
    .AsAIAgent(instructions: "You are good at telling jokes.", name: "Joker");

Console.WriteLine(await jokerAgent.RunAsync("你好我叫西瓜，爱吃苹果") + Environment.NewLine);
await foreach (var response in jokerAgent.RunStreamingAsync("请介绍下你自己"))
{
    Console.Write(response);
}
