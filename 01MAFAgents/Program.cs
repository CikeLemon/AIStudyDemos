
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

var agent = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(model)
    .AsAIAgent(
        new ChatClientAgentOptions
        {
             Name="jocker",
             ChatOptions= new ChatOptions
             {
                 Tools = [AIFunctionFactory.Create(WeatherServicePlugin.GetCurrentWeatherAsync)],
             },
        }, chatClient =>
        {
         return   chatClient.AsBuilder()
            .UseAIContextProviders()
                .Build();
        });

Console.WriteLine(await agent.RunAsync("请告诉我深圳天气咋样？"));

public class WeatherServicePlugin
{
    [Description("Get current weather for a location")]
    public static async Task<string> GetCurrentWeatherAsync(string location)
    {
        return $"{location} 今天 15℃";
    }
}

