
using AI.Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(AICommon).Assembly)
    .Build();

string baseUrl = configuration.GetSection("AIEndpoint").Value!;
string apiKey = configuration.GetSection("AIApiKey").Value!;
const string model = "glm-5.1";

HttpClientInterceptor.StartInterception();

//var jokerAgent = new OpenAIClient(
//        new ApiKeyCredential(apiKey),
//        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
//    .GetChatClient(model)
//    .AsAIAgent(instructions: "你是个乐于助人的 AI 助手", name: "Joker");

//var memorySession1 = await jokerAgent.CreateSessionAsync();
//Console.WriteLine(await jokerAgent.RunAsync("你好我叫西瓜，爱吃苹果", memorySession1) + Environment.NewLine);
//Console.WriteLine(await jokerAgent.RunAsync("我叫啥，喜欢吃啥", memorySession1) + Environment.NewLine);



var jokerAgent = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(model)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "Joker",
        ChatHistoryProvider = new InMemoryChatHistoryProvider()
    });

var memorySession1 = await jokerAgent.CreateSessionAsync();
Console.WriteLine(await jokerAgent.RunAsync("你好我叫西瓜，爱吃苹果", memorySession1) + Environment.NewLine);
Console.WriteLine(await jokerAgent.RunAsync("我叫啥，喜欢吃啥", memorySession1) + Environment.NewLine);

Console.WriteLine(await jokerAgent.SerializeSessionAsync(memorySession1));


internal sealed class SimpleServiceMemoryProvider : AIContextProvider
{
    private readonly ProviderSessionState<State> _sessionState;
    private readonly ServiceClient _client;

    public SimpleServiceMemoryProvider(ServiceClient client, Func<AgentSession?, State>? stateInitializer = null)
        : base(null, null)
    {
        this._sessionState = new ProviderSessionState<State>(
            stateInitializer ?? (_ => new State()),
            this.GetType().Name);
        this._client = client;
    }

    public override string StateKey => this._sessionState.StateKey;

    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        var state = this._sessionState.GetOrInitializeState(context.Session);

        if (state.MemoriesId == null)
        {
            // No stored memories yet.
            return new ValueTask<AIContext>(new AIContext());
        }

        // Find memories that match the current user input.
        var memories = this._client.LoadMemories(state.MemoriesId, string.Join("\n", context.AIContext.Messages?.Select(x => x.Text) ?? []));

        // Return a new message that contains the text from any memories that were found.
        return new ValueTask<AIContext>(new AIContext
        {
            Messages = [new ChatMessage(ChatRole.User, "Here are some memories to help answer the user question: " + string.Join("\n", memories.Select(x => x.Text)))]
        });
    }

    protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        var state = this._sessionState.GetOrInitializeState(context.Session);
        // Create a memory container in the service for this session
        // and save the returned id in the session.
        state.MemoriesId ??= this._client.CreateMemoryContainer();
        this._sessionState.SaveState(context.Session, state);

        // Use the service to extract memories from the user input and agent response.
        await this._client.StoreMemoriesAsync(state.MemoriesId, context.RequestMessages.Concat(context.ResponseMessages ?? []), cancellationToken);
    }

    public class State
    {
        public string? MemoriesId { get; set; }
    }
}