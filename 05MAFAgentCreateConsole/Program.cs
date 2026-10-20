// ============================================================
// MAF (Microsoft Agent Framework) —— 创建 Agent 的多种方式
//
// 方式一：AsAIAgent 扩展方法一行创建（最简单）
// 方式二：ChatClientAgentOptions 完整配置创建（指令/温度/工具）
// 方式三：直接 new ChatClientAgent（手动实例化，可完全控制）
// 方式四：ChatClientBuilder 管道创建（显式组装中间件后 Build 成 Agent）
// 方式五：AIAgentBuilder 装饰已有 Agent（套一层自定义中间件/包装器）
// 方式六：结构化输出 Agent（RunAsync<T> 直接返回强类型结果）
// ============================================================

using AI.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(AICommon).Assembly)
    .Build();

string baseUrl = configuration.GetSection("AIEndpoint").Value!;
string apiKey = configuration.GetSection("AIApiKey").Value!;
const string model = "glm-5.1";

HttpClientInterceptor.StartInterception();

// 公共步骤：OpenAI 兼容端点 -> MAF 的 IChatClient
IChatClient CreateChatClient() => new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(model)
    .AsIChatClient();

// ------------------------------------------------------------
// 方式一：AsAIAgent 一行创建，适合最简单的对话场景
// ------------------------------------------------------------
AIAgent simpleAgent = CreateChatClient().AsAIAgent(
    name: "Joker",
    instructions: "你是一位简洁的中文AI助手，回答不超过一句话。");

Console.WriteLine($"[方式一 AsAIAgent] {await simpleAgent.RunAsync("用一句话介绍你自己")}");

// ------------------------------------------------------------
// 方式二：ChatClientAgentOptions 完整配置创建
// 指令、温度、工具都收拢在 ChatOptions 中，配置项最全
// ------------------------------------------------------------
AIAgent weatherAgent = CreateChatClient().AsAIAgent(new ChatClientAgentOptions
{
    Name = "WeatherAgent",
    Description = "一个可以查询天气的智能体",
    ChatOptions = new ChatOptions
    {
        Instructions = "你是天气小助手，需要查询天气时调用工具。",
        Temperature = 0.3f,
        Tools = [AIFunctionFactory.Create(WeatherServicePlugin.GetCurrentWeatherAsync)],
    },
});

Console.WriteLine($"[方式二 ChatClientAgentOptions] {await weatherAgent.RunAsync("广州今天天气怎么样？")}");

// ------------------------------------------------------------
// 方式三：直接 new ChatClientAgent
// 与方式二等价，但不再依赖扩展方法，适合需要自己管理生命周期的场景
// ------------------------------------------------------------
var chatClientAgent = new ChatClientAgent(CreateChatClient(), new ChatClientAgentOptions
{
    Name = "Echo",
    Description = "演示手动实例化的 Agent",
    ChatOptions = new ChatOptions { Instructions = "把用户输入原样重复一遍。" },
});

Console.WriteLine($"[方式三 new ChatClientAgent] Name={chatClientAgent.Name}, Id={chatClientAgent.Id}, Description={chatClientAgent.Description}");
Console.WriteLine($"[方式三 运行] {await chatClientAgent.RunAsync("Hello MAF")}");

// ------------------------------------------------------------
// 方式四：ChatClientBuilder 管道创建
// 先用中间件组装 IChatClient 管道（这里开启工具自动调用），
// 再 BuildAIAgent 得到 Agent。
// 注意：ChatClientAgent 默认内部已自动包了 FunctionInvocation，
// UseProvidedChatClientAsIs = true 时才会按你的管道原样使用。
// ------------------------------------------------------------
AIAgent pipelineAgent = new ChatClientBuilder(CreateChatClient())
    .UseFunctionInvocation()
    .BuildAIAgent(new ChatClientAgentOptions
    {
        Name = "WeatherPipelineAgent",
        ChatOptions = new ChatOptions
        {
            Instructions = "你是天气小助手，需要查询天气时调用工具。",
            Tools = [AIFunctionFactory.Create(WeatherServicePlugin.GetCurrentWeatherAsync)],
        },
    });

Console.WriteLine($"[方式四 ChatClientBuilder] {await pipelineAgent.RunAsync("上海天气怎么样？")}");

// ------------------------------------------------------------
// 方式五：AIAgentBuilder 装饰已有 Agent
// AsBuilder() 把现成 Agent 变成可包装的管道，Use() 套一层装饰器，
// 官方还内置了 UseLogging / UseOpenTelemetry / UseToolApproval 等装饰器。
// ------------------------------------------------------------
AIAgent decoratedAgent = simpleAgent.AsBuilder()
    .Use(inner => new UsageCountingAgent(inner))
    .Build();

await decoratedAgent.RunAsync("北京有多少人口？");
await decoratedAgent.RunAsync("深圳呢？");
Console.WriteLine($"[方式五 AIAgentBuilder] 装饰器统计到共被调用 {((UsageCountingAgent)decoratedAgent).RunCount} 次");

// ------------------------------------------------------------
// 方式六：结构化输出 Agent，RunAsync<T> 直接返回强类型结果
// ------------------------------------------------------------
AIAgent extractorAgent = CreateChatClient().AsAIAgent(
    name: "InfoExtractor",
    instructions: "从用户输入中提取个人信息。");

AgentResponse<PersonInfo> response = await extractorAgent.RunAsync<PersonInfo>("我叫西瓜，今年28岁，住在深圳，爱吃苹果和香蕉");
PersonInfo info = response.Result;
Console.WriteLine($"[方式六 结构化输出] 姓名={info.Name}, 年龄={info.Age}, 城市={info.City}, 爱好={string.Join("、", info.Hobbies)}");

/// <summary>天气查询工具插件。</summary>
public class WeatherServicePlugin
{
    [Description("获取指定城市的当前天气")]
    public static Task<string> GetCurrentWeatherAsync(string location)
    {
        return Task.FromResult($"{location} 今天 15℃，多云。");
    }
}

/// <summary>自定义装饰器：统计 Agent 被调用的次数，演示 AIAgentBuilder.Use 的包装能力。</summary>
internal sealed class UsageCountingAgent(AIAgent innerAgent) : DelegatingAIAgent(innerAgent)
{
    public int RunCount { get; private set; }

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken = default)
    {
        RunCount++;
        Console.WriteLine($"    [UsageCountingAgent] 第 {RunCount} 次拦截调用");
        return await base.RunCoreAsync(messages, session, options, cancellationToken);
    }
}

/// <summary>结构化输出的目标类型。</summary>
public sealed class PersonInfo
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string City { get; set; } = string.Empty;
    public List<string> Hobbies { get; set; } = [];
}
