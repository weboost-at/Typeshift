using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// Runs <typeparamref name="TAgent"/> with the services of its scope: its tools and its provider connector resolve
/// there. Registered as transient by <c>AddTypeshift()</c>: inject it, or resolve it from a scope, never from the root
/// provider. <see cref="ITypeshiftRuntime.CreateHost{TAgent}"/> creates one in a scope of its own, which disposing the
/// host disposes. Its runs, answers, resumes and <c>ResolveOptionsAsync</c> are its own, so code generic over the agent
/// calls them too. The generator adds the agent's typed members: the same runs with the agent's <c>Settings</c> for
/// the run, and <c>LoadSettingsAsync</c>, <c>SaveSettingsAsync</c> and <c>UpdateSettingsAsync</c>.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/generated-members#the-agent-host">The agent host</see>.
/// </summary>
/// <typeparam name="TAgent">The agent.</typeparam>
public sealed partial class AgentHost<TAgent> : IAgentHost, IAsyncDisposable, IDisposable where TAgent : Agent
{
    // D25: the services of the scope it was resolved from, or of the one the runtime created for it (D76)
    private readonly IServiceProvider _services;

    // D76: only a host the runtime created owns its scope; one the container resolved leaves it to the container
    private readonly AsyncServiceScope? _scope;

    // D72: the ids WithSettingsId and WithConversationId gave, each with its type
    private StoreId? _settingsId;
    private StoreId? _conversationId;

    // D87: the provider and the model the call site gave, over every settings layer; D89: and its model policy
    private ProviderId? _provider;
    private string? _model;
    private ModelPolicy? _policy;

    internal AgentHost(TAgent agent, IServiceProvider services, AsyncServiceScope? scope = null)
    {
        Agent = agent;
        _services = services;
        _scope = scope;
    }

    internal AgentHost(TAgent agent, AsyncServiceScope scope) : this(agent, scope.ServiceProvider, scope)
    {
    }

    // D76: built rather than resolved where the library makes a host, since a scope keeps every disposable the
    // container builds until it ends
    internal static AgentHost<TAgent> In(IServiceProvider services) => new(Registered(services), services);

    // D1: a module registers the agents the generator wrote code for; the container's own message names no cause
    internal static TAgent Registered(IServiceProvider services) =>
        services.GetService<TAgent>()
        ?? throw new InvalidOperationException(
            $"The agent '{typeof(TAgent).Name}' is not registered. AddTypeshift() and TypeshiftRuntime.Create(...) "
            + "register the agents of the project that calls them and of the projects it references: call one in "
            + "your app, not only in a library it references. The generator also leaves out an agent that is "
            + "abstract, one that names its base through a using alias (TSH017) and one of a project built with "
            + "another Typeshift version (TSH020).");

    // D25: the host the container resolves. From the root it would run every customer on one instance of each scoped
    // service, where scopes are not validated, and the root would keep it until the app stops, so it is refused.
    // The root hands a factory the provider the runtime holds, a scope another
    internal static AgentHost<TAgent> Resolved(IServiceProvider services) =>
        ReferenceEquals(services, services.GetRequiredService<ITypeshiftRuntime>().Services)
            ? throw new InvalidOperationException(
                $"AgentHost<{typeof(TAgent).Name}> was resolved from the root provider, as a host injected into a "
                + "singleton is. Its runs would share one instance of each scoped service, such as the current "
                + "customer, and the root would keep every host until the app stops: inject "
                + $"ITypeshiftRuntime and call CreateHost<{typeof(TAgent).Name}>() for each unit of work, or resolve "
                + "the host from a scope.")
            : In(services);

    /// <summary>
    /// The agent as its code declares it.
    /// </summary>
    public TAgent Agent { get; }

    // D76: a job fills the scope of its unit of work here, as a request's middleware fills the request's
    /// <summary>
    /// The services the host's runs use: those of the scope it was resolved from, or of the scope
    /// <see cref="ITypeshiftRuntime.CreateHost{TAgent}"/> created for it. A job sets scoped state through them before
    /// it runs, such as the current tenant its tools read.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/using-an-agent#run-from-a-background-job">Run from a
    /// background job</see>.
    /// </summary>
    public IServiceProvider Services => _services;

    /// <summary>
    /// Returns a copy of this host with a settings id and leaves this host as it was. The copy's runs apply the
    /// settings stored under the id, over the agent's own, and its settings members load, save and configure them.
    /// The id's type picks the store: an <see cref="ISettingsStore{TId}"/> of that type.
    /// </summary>
    /// <param name="id">The id of the stored settings.</param>
    /// <typeparam name="TId">The id's type.</typeparam>
    /// <returns>
    /// The copy, with this host's conversation id and in its scope. Disposing it disposes what disposing this host
    /// would.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    public AgentHost<TAgent> WithSettingsId<TId>(TId id) where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(id);
        return Copy(new StoreId<TId>(id), _conversationId, _provider, _model, _policy);
    }

    // D72: the id's type is its value's, so a Guid? names the settings under the Guid
    /// <summary>
    /// Returns a copy of this host with the value of a settings id of a nullable value type, as a request's
    /// <c>Guid?</c> holds it, and leaves this host as it was. The copy is the one the value itself gives.
    /// </summary>
    /// <param name="id">The id of the stored settings; it must hold a value.</param>
    /// <typeparam name="TId">The id's type without the <c>?</c>.</typeparam>
    /// <returns>
    /// The copy, with this host's conversation id and in its scope. Disposing it disposes what disposing this host
    /// would.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> holds no value.</exception>
    public AgentHost<TAgent> WithSettingsId<TId>(TId? id) where TId : struct =>
        Copy(
            new StoreId<TId>(id ?? throw new ArgumentNullException(nameof(id))),
            _conversationId,
            _provider,
            _model,
            _policy);

    /// <summary>
    /// Returns a copy of this host with a conversation id and leaves this host as it was. The copy's runs resume the
    /// conversation and store what they add to it. The id's type picks the store: an
    /// <see cref="IConversationStore{TId}"/> of that type.
    /// </summary>
    /// <param name="id">The conversation's id.</param>
    /// <typeparam name="TId">The id's type.</typeparam>
    /// <returns>
    /// The copy, with this host's settings id and in its scope. Disposing it disposes what disposing this host would.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    public AgentHost<TAgent> WithConversationId<TId>(TId id) where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(id);
        return Copy(_settingsId, new StoreId<TId>(id), _provider, _model, _policy);
    }

    // D72: the id's type is its value's, so a Guid? names the conversation under the Guid
    /// <summary>
    /// Returns a copy of this host with the value of a conversation id of a nullable value type, as a request's
    /// <c>Guid?</c> holds it, and leaves this host as it was. The copy is the one the value itself gives.
    /// </summary>
    /// <param name="id">The conversation's id; it must hold a value.</param>
    /// <typeparam name="TId">The id's type without the <c>?</c>.</typeparam>
    /// <returns>
    /// The copy, with this host's settings id and in its scope. Disposing it disposes what disposing this host would.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> holds no value.</exception>
    public AgentHost<TAgent> WithConversationId<TId>(TId? id) where TId : struct =>
        Copy(
            _settingsId,
            new StoreId<TId>(id ?? throw new ArgumentNullException(nameof(id))),
            _provider,
            _model,
            _policy);

    // D87: the call site's choice of who serves the run, apart from the model, as ids are given (D72)
    /// <summary>
    /// Returns a copy of this host whose runs a provider serves, named by its key, and leaves this host as it was: the
    /// client registered under the key, as <c>services.AddKeyedChatClient(key, client)</c> registers one. It wins over
    /// the provider the agent's code and settings name, and serves the whole run: its sub-agents, the agents it hands
    /// off to and its summary, while each sub-agent keeps its own model. A <see cref="ChatProvider"/>, a record of
    /// yours that derives from it included, is taken as the document it is, as
    /// <see cref="WithProvider(ChatProvider)"/> takes it.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/models#choosing-at-the-call-site">Choosing at the call
    /// site</see>.
    /// </summary>
    /// <param name="provider">The provider's key.</param>
    /// <typeparam name="TProviderId">The key's type, any type the app registered a client under.</typeparam>
    /// <returns>
    /// The copy, with this host's ids and model and in its scope. Disposing it disposes what disposing this host would.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="provider"/> is text that is white space, or a document whose <c>Kind</c> is.
    /// </exception>
    public AgentHost<TAgent> WithProvider<TProviderId>(TProviderId provider) where TProviderId : notnull
    {
        ArgumentNullException.ThrowIfNull(provider);

        // D88: a record derived from ChatProvider binds here, not to the overload that takes one
        if (provider is ChatProvider document) return WithProvider(document);
        if (provider is string text) ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(provider));

        return Copy(_settingsId, _conversationId, new ProviderId<TProviderId>(provider), _model, _policy);
    }

    // D88: a provider of the app's own, which its connector turns into a client that Typeshift keeps
    /// <summary>
    /// Returns a copy of this host whose runs a provider your app owns serves, such as a customer's own account, and
    /// leaves this host as it was. Your <see cref="IChatProviderConnector"/> turns the document into a client, once for
    /// every equal document. It wins over the provider the agent's code and settings name, serves the whole run as a
    /// key does, and its <c>Model</c> answers a run that names no model.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/models#providers-your-customers-bring">Providers your
    /// customers bring</see>.
    /// </summary>
    /// <param name="provider">The document, as your app loaded it.</param>
    /// <returns>
    /// The copy, with this host's ids and model and in its scope. Disposing it disposes what disposing this host would.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="ArgumentException">The document's <c>Kind</c> is null or white space.</exception>
    public AgentHost<TAgent> WithProvider(ChatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider.Kind, nameof(provider));

        return Copy(_settingsId, _conversationId, new ProviderDocument(provider), _model, _policy);
    }

    /// <summary>
    /// Returns a copy of this host whose runs ask for a model, and leaves this host as it was. It wins over the model
    /// the agent's code and settings name, whether or not the agent makes its model configurable, and is the host's
    /// agent's alone: its sub-agents and the agents it hands off to keep their own.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/models#choosing-at-the-call-site">Choosing at the call
    /// site</see>.
    /// </summary>
    /// <param name="model">The model, as the API of the provider that serves the run names it.</param>
    /// <returns>
    /// The copy, with this host's ids and provider and in its scope. Disposing it disposes what disposing this host
    /// would.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="model"/> is null or white space.</exception>
    public AgentHost<TAgent> WithModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return Copy(_settingsId, _conversationId, _provider, model, _policy);
    }

    // D89: the call site's policy, over the one the agent's code declares, as its provider is (D87)
    /// <summary>
    /// Returns a copy of this host whose runs go by a model policy in place of the one the agent's code declares, and
    /// leaves this host as it was: what a run does when a model call fails, as in
    /// <c>support.WithModelPolicy(support.Agent.ModelPolicy.Timeout(patience))</c>. It serves the whole run, as a
    /// provider the call site names does: its sub-agents, the agents it hands off to and its summary.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/models#the-model-policy">The model policy</see>.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <returns>
    /// The copy, with this host's ids, provider and model and in its scope. Disposing it disposes what disposing this
    /// host would.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    [Experimental(ExperimentalIds.ModelPolicy, UrlFormat = ExperimentalIds.ModelPolicyUrl)]
    public AgentHost<TAgent> WithModelPolicy(ModelPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return Copy(_settingsId, _conversationId, _provider, _model, policy);
    }

    // D72, D76: a copy runs in this host's scope and disposes it as this host would; a scope's disposal is idempotent
    private AgentHost<TAgent> Copy(
        StoreId? settingsId,
        StoreId? conversationId,
        ProviderId? provider,
        string? model,
        ModelPolicy? policy) =>
        new(Agent, _services, _scope)
        {
            _settingsId = settingsId,
            _conversationId = conversationId,
            _provider = provider,
            _model = model,
            _policy = policy
        };

    // D12: the runs without the agent's Settings are the host's own, so code generic over the agent runs any agent
    // and takes the token positionally; the generated members add the forms with Settings
    /// <summary>
    /// Runs the agent on a message, in the host's conversation if it has one, with the stored settings over the
    /// agent's code. The agent's generated <c>RunAsync</c> also takes the run's <c>Settings</c>.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/running-agents">Running an agent</see>.
    /// </summary>
    /// <param name="input">The user's message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response; its messages carry the agent's name as their author.</returns>
    /// <exception cref="DecisionUnavailableException">
    /// A call would need a decision of your app, and the run has no conversation to keep it in. The run throws
    /// before it calls the model, or, with approval rules registered, at a call no rule decided.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The agent's chat client runs a tool loop of its own, or something the run needs is not registered, such as a
    /// chat client or a store for one of the host's ids.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public Task<ChatResponse> RunAsync(string input, CancellationToken ct = default) =>
        RunCoreAsync(new ChatMessage(ChatRole.User, input), null, ct);

    /// <summary>
    /// Runs the agent on a message, which may carry images, as <see cref="RunAsync(string, CancellationToken)"/> does.
    /// </summary>
    /// <param name="input">The user's message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response; its messages carry the agent's name as their author.</returns>
    /// <exception cref="ArgumentException">
    /// The message carries approval contents, which a run does not take: answers go through <c>AnswerAsync</c>.
    /// </exception>
    /// <exception cref="DecisionUnavailableException">
    /// A call would need a decision of your app, and the run has no conversation to keep it in. The run throws
    /// before it calls the model, or, with approval rules registered, at a call no rule decided.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The agent's chat client runs a tool loop of its own, or something the run needs is not registered.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public Task<ChatResponse> RunAsync(ChatMessage input, CancellationToken ct = default) =>
        RunCoreAsync(input, null, ct);

    /// <summary>
    /// Runs the agent on a message as <see cref="RunAsync(string, CancellationToken)"/> does, asking the model for JSON
    /// of <typeparamref name="TResult"/>'s schema.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="input">The user's message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result.</returns>
    /// <exception cref="DecisionUnavailableException">
    /// A call would need a decision of your app, and the run has no conversation to keep it in. The run throws
    /// before it calls the model, or, with approval rules registered, at a call no rule decided.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The agent's chat client runs a tool loop of its own, or something the run needs is not registered.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public Task<ChatResponse<TResult>> RunAsync<TResult>(string input, CancellationToken ct = default) =>
        RunCoreAsync<TResult>(new ChatMessage(ChatRole.User, input), null, ct);

    /// <summary>
    /// Runs the agent on a message, which may carry images, as
    /// <see cref="RunAsync{TResult}(string, CancellationToken)"/> does.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="input">The user's message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result.</returns>
    /// <exception cref="ArgumentException">
    /// The message carries approval contents, which a run does not take: answers go through <c>AnswerAsync</c>.
    /// </exception>
    /// <exception cref="DecisionUnavailableException">
    /// A call would need a decision of your app, and the run has no conversation to keep it in. The run throws
    /// before it calls the model, or, with approval rules registered, at a call no rule decided.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The agent's chat client runs a tool loop of its own, or something the run needs is not registered.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public Task<ChatResponse<TResult>> RunAsync<TResult>(ChatMessage input, CancellationToken ct = default) =>
        RunCoreAsync<TResult>(input, null, ct);

    /// <summary>
    /// Runs the agent on a message as <see cref="RunAsync(string, CancellationToken)"/> does, streaming the updates.
    /// The agent's generated <c>RunStreamingAsync</c> also takes the run's <c>Settings</c>.
    /// </summary>
    /// <param name="input">The user's message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>
    /// The run, which streams its updates, each with the agent's name as its author, and then holds its response.
    /// </returns>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. A stream throws it when its updates are read.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The agent's chat client runs a tool loop of its own, or something the run needs is not registered.
    /// </exception>
    public StreamingRun RunStreamingAsync(string input, CancellationToken ct = default) =>
        RunStreamingCoreAsync(new ChatMessage(ChatRole.User, input), null, ct);

    /// <summary>
    /// Runs the agent on a message, which may carry images, as
    /// <see cref="RunStreamingAsync(string, CancellationToken)"/> does.
    /// </summary>
    /// <param name="input">The user's message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>
    /// The run, which streams its updates, each with the agent's name as its author, and then holds its response.
    /// </returns>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. A stream throws it when its updates are read.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The agent's chat client runs a tool loop of its own, or something the run needs is not registered.
    /// </exception>
    public StreamingRun RunStreamingAsync(ChatMessage input, CancellationToken ct = default) =>
        RunStreamingCoreAsync(input, null, ct);

    /// <summary>
    /// Runs the agent on a message as <see cref="RunStreamingAsync(string, CancellationToken)"/> does, asking the model
    /// for JSON of <typeparamref name="TResult"/>'s schema. Once its updates are read to the end, its <c>Response</c>
    /// holds the result.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="input">The user's message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The run, which streams its updates and then holds the result.</returns>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. A stream throws it when its updates are read.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The agent's chat client runs a tool loop of its own, or something the run needs is not registered.
    /// </exception>
    public StreamingRun<TResult> RunStreamingAsync<TResult>(string input, CancellationToken ct = default) =>
        RunStreamingCoreAsync<TResult>(new ChatMessage(ChatRole.User, input), null, ct);

    /// <summary>
    /// Runs the agent on a message, which may carry images, as
    /// <see cref="RunStreamingAsync{TResult}(string, CancellationToken)"/> does.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="input">The user's message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The run, which streams its updates and then holds the result.</returns>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. A stream throws it when its updates are read.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The agent's chat client runs a tool loop of its own, or something the run needs is not registered.
    /// </exception>
    public StreamingRun<TResult> RunStreamingAsync<TResult>(ChatMessage input, CancellationToken ct = default) =>
        RunStreamingCoreAsync<TResult>(input, null, ct);

    /// <summary>
    /// Records your app's answers to decisions of the host's conversation, from <c>WithConversationId</c>, and, once
    /// no decision is left open, carries them out and continues the run. With decisions left open it calls no model,
    /// and the response lists them. The agent's generated <c>AnswerAsync</c> also takes the run's <c>Settings</c>.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/tool-approval#approving-or-rejecting">Approving or
    /// rejecting</see>.
    /// </summary>
    /// <param name="answers">The answers, each made from its decision, as <c>decision.Approve()</c> makes one.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response; with decisions left open, one that lists them.</returns>
    /// <exception cref="DecisionClosedException">
    /// An answer names a decision that is not open. Nothing of the call is recorded.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An answer does not fit its decision, one decision is answered twice, or there is no answer. Nothing is
    /// recorded.
    /// </exception>
    /// <exception cref="StoreConflictException">
    /// Other saves of the conversation's decisions came first ten times in a row.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. The answers are recorded: continue with
    /// <see cref="ResumeAsync(CancellationToken)"/>.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="answers"/> is null.</exception>
    public Task<ChatResponse> AnswerAsync(IReadOnlyList<DecisionAnswer> answers, CancellationToken ct = default) =>
        AnswerCoreAsync(answers, null, ct);

    /// <summary>
    /// Records your app's answers and continues the run as
    /// <see cref="AnswerAsync(IReadOnlyList{DecisionAnswer}, CancellationToken)"/> does, asking the model for JSON of
    /// <typeparamref name="TResult"/>'s schema.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="answers">The answers, each made from its decision.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result; with decisions left open, one that lists them and has none.</returns>
    /// <exception cref="DecisionClosedException">An answer names a decision that is not open.</exception>
    /// <exception cref="ArgumentException">An answer does not fit its decision, or there is none.</exception>
    /// <exception cref="StoreConflictException">
    /// Other saves of the conversation's decisions came first ten times in a row.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. The answers are recorded.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="answers"/> is null.</exception>
    public Task<ChatResponse<TResult>> AnswerAsync<TResult>(
        IReadOnlyList<DecisionAnswer> answers,
        CancellationToken ct = default) =>
        AnswerCoreAsync<TResult>(answers, null, ct);

    /// <summary>
    /// Records your app's answers and continues the run as
    /// <see cref="AnswerAsync(IReadOnlyList{DecisionAnswer}, CancellationToken)"/> does, streaming the updates. The
    /// answers are recorded when the updates are read.
    /// </summary>
    /// <param name="answers">The answers, each made from its decision.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>
    /// The run, which streams its updates and then holds its response; with decisions left open, one update that lists
    /// them.
    /// </returns>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. The answers are recorded.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    public StreamingRun AnswerStreamingAsync(
        IReadOnlyList<DecisionAnswer> answers,
        CancellationToken ct = default) =>
        AnswerStreamingCoreAsync(answers, null, ct);

    /// <summary>
    /// Records your app's answers and continues the run as
    /// <see cref="AnswerStreamingAsync(IReadOnlyList{DecisionAnswer}, CancellationToken)"/> does, asking the model for
    /// JSON of <typeparamref name="TResult"/>'s schema.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="answers">The answers, each made from its decision.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The run, which streams its updates and then holds the result.</returns>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. The answers are recorded.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    public StreamingRun<TResult> AnswerStreamingAsync<TResult>(
        IReadOnlyList<DecisionAnswer> answers,
        CancellationToken ct = default) =>
        AnswerStreamingCoreAsync<TResult>(answers, null, ct);

    /// <summary>
    /// Continues the host's conversation, from <c>WithConversationId</c>, without new input: the run that was
    /// interrupted, from its last stored step, or the decisions your app answered. While decisions are still open it
    /// calls no model, and the response lists them. The agent's generated <c>ResumeAsync</c> also takes the run's
    /// <c>Settings</c>.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/conversations#resuming">Resuming</see>.
    /// </summary>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    /// <exception cref="NothingToResumeException">
    /// The conversation has nothing to continue: its last run ended, and no decision your app answered or withdrew
    /// waits.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public Task<ChatResponse> ResumeAsync(CancellationToken ct = default) => RunCoreAsync(null, null, ct);

    /// <summary>
    /// Continues the host's conversation as <see cref="ResumeAsync(CancellationToken)"/> does, streaming the updates.
    /// </summary>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The run, which streams its updates and then holds its response.</returns>
    /// <exception cref="NothingToResumeException">
    /// The conversation has nothing to continue, when the updates are read.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. A stream throws it when its updates are read.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    public StreamingRun ResumeStreamingAsync(CancellationToken ct = default) =>
        RunStreamingCoreAsync(null, null, ct);

    // D64: a typed run a crash cut off resumes typed, so its answer is read as the run that began it asked
    /// <summary>
    /// Continues the host's conversation as <see cref="ResumeAsync(CancellationToken)"/> does, asking the model for
    /// JSON of <typeparamref name="TResult"/>'s schema, as the typed run that was interrupted did.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result; while decisions are open, one that lists them and has none.</returns>
    /// <exception cref="NothingToResumeException">
    /// The conversation has nothing to continue: its last run ended, and no decision your app answered or withdrew
    /// waits.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public Task<ChatResponse<TResult>> ResumeAsync<TResult>(CancellationToken ct = default) =>
        RunCoreAsync<TResult>(null, null, ct);

    /// <summary>
    /// Continues the host's conversation as <see cref="ResumeAsync{TResult}(CancellationToken)"/> does, streaming the
    /// updates. Once they are read to the end, its <c>Response</c> holds the result.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The run, which streams its updates and then holds the result.</returns>
    /// <exception cref="NothingToResumeException">
    /// The conversation has nothing to continue, when the updates are read.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. A stream throws it when its updates are read.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    public StreamingRun<TResult> ResumeStreamingAsync<TResult>(CancellationToken ct = default) =>
        RunStreamingCoreAsync<TResult>(null, null, ct);

    // D50: the input of a run that failed is stored, so a retry sends it once more without storing it again
    /// <summary>
    /// Continues the host's conversation, from <c>WithConversationId</c>, as
    /// <see cref="ResumeAsync(CancellationToken)"/> does, and also when its last run failed, was cancelled or stopped,
    /// as on a provider's error or a timeout: from that run's stored steps, so the model receives the user's message
    /// once. A retry on another model or provider runs on a copy of the host, as from <c>WithModel(model)</c> or
    /// <c>WithProvider(key)</c>. The agent's generated <c>RetryAsync</c> also takes the run's <c>Settings</c>.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/conversations#retrying-a-failed-run">Retrying a
    /// failed run</see>.
    /// </summary>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    /// <exception cref="NothingToResumeException">
    /// The conversation has nothing to retry: its last run answered or waits for your app, and no decision your app
    /// answered or withdrew waits.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public Task<ChatResponse> RetryAsync(CancellationToken ct = default) => RunCoreAsync(null, null, ct, retries: true);

    /// <summary>
    /// Continues the host's conversation as <see cref="RetryAsync(CancellationToken)"/> does, streaming the updates.
    /// </summary>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The run, which streams its updates and then holds its response.</returns>
    /// <exception cref="NothingToResumeException">
    /// The conversation has nothing to retry, when the updates are read.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. A stream throws it when its updates are read.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    public StreamingRun RetryStreamingAsync(CancellationToken ct = default) =>
        RunStreamingCoreAsync(null, null, ct, retries: true);

    /// <summary>
    /// Continues the host's conversation as <see cref="RetryAsync(CancellationToken)"/> does, asking the model for
    /// JSON of <typeparamref name="TResult"/>'s schema, as the typed run that failed did.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result; while decisions are open, one that lists them and has none.</returns>
    /// <exception cref="NothingToResumeException">
    /// The conversation has nothing to retry: its last run answered or waits for your app, and no decision your app
    /// answered or withdrew waits.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public Task<ChatResponse<TResult>> RetryAsync<TResult>(CancellationToken ct = default) =>
        RunCoreAsync<TResult>(null, null, ct, retries: true);

    /// <summary>
    /// Continues the host's conversation as <see cref="RetryAsync{TResult}(CancellationToken)"/> does, streaming the
    /// updates. Once they are read to the end, its <c>Response</c> holds the result.
    /// </summary>
    /// <typeparam name="TResult">The result's type.</typeparam>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The run, which streams its updates and then holds the result.</returns>
    /// <exception cref="NothingToResumeException">
    /// The conversation has nothing to retry, when the updates are read.
    /// </exception>
    /// <exception cref="ConversationBusyException">
    /// A run of the host's conversation is going on in this container, or the store refused a step of this run
    /// because a run in another container appended first. A stream throws it when its updates are read.
    /// </exception>
    /// <exception cref="ConversationStoreException">
    /// The store failed to append a step of the run, unless <c>TypeshiftOptions.StoreFailure</c> lets it continue,
    /// or to keep a decision the run asks.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, the agent's chat client runs a tool loop of its own, or something the run needs
    /// is not registered.
    /// </exception>
    public StreamingRun<TResult> RetryStreamingAsync<TResult>(CancellationToken ct = default) =>
        RunStreamingCoreAsync<TResult>(null, null, ct, retries: true);

    /// <summary>
    /// Resolves the options of a run, for the raw path:
    /// <see cref="CreateChatClientAsync(ChatOptions, CancellationToken)"/>, then the client. The options carry the
    /// host's conversation. The agent's generated <c>ResolveOptionsAsync</c> also takes the run's <c>Settings</c>.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/running-agents#the-raw-path">The raw path</see>.
    /// </summary>
    /// <param name="ct">Cancels the resolution.</param>
    /// <returns>The options, with the agent's tools.</returns>
    /// <exception cref="InvalidOperationException">
    /// Something the options need is not registered: a store for the type of the host's settings id, or an
    /// <see cref="IMcpToolProvider"/> for the agent's MCP servers.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public Task<ChatOptions> ResolveOptionsAsync(CancellationToken ct = default) =>
        ResolveOptionsCoreAsync(_settingsId, null, ct);

    // The settings id WithSettingsId gave, which the settings members use in place of the agent's own settings
    internal StoreId? SettingsId => _settingsId;

    private StoreId ResumedConversation =>
        _conversationId
        ?? throw new InvalidOperationException(
            "A resume needs a conversation: resume on a host from WithConversationId(id).");

    // D80: decisions belong to a conversation
    private StoreId DecidedConversation =>
        _conversationId
        ?? throw new InvalidOperationException(
            "Decisions belong to a conversation: call this on a host from WithConversationId(id).");

#pragma warning disable RS0030 // the fallback for a host that registered no TimeProvider
    private TimeProvider Time => _services.GetService<TimeProvider>() ?? TimeProvider.System;
#pragma warning restore RS0030

    private DecisionLedger Ledger => new(Store, Time);

    // D69, D72: through the store of the id's type, so the memory a new app runs on answers too
    /// <summary>
    /// Loads the history of the host's conversation, from <c>WithConversationId</c>: every entry its runs stored, in
    /// order, for a chat page that reloads or a job that looks for interrupted runs. It starts no run.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/conversations#what-is-stored">What is stored</see>.
    /// </summary>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The entries; none for a conversation that never ran.</returns>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, or stores are registered and none keeps conversations under its id's type.
    /// </exception>
    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(CancellationToken ct = default)
    {
        SynchronizationContexts.Leave();

        var conversation = _conversationId
            ?? throw new InvalidOperationException(
                "A conversation's history needs a conversation: load it on a host from WithConversationId(id).");

        return await conversation.LoadConversationAsync(Store, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads what the host's conversation, from <c>WithConversationId</c>, waits for: the decisions your app has not
    /// answered yet, whether the answered ones are ready to be carried out, the tools approved for the conversation
    /// and its mode. It reads the conversation's decisions and none of its history, and starts no run.
    /// </summary>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The decisions; none open for a conversation that never asked.</returns>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    public async Task<ConversationDecisions> GetDecisionsAsync(CancellationToken ct = default)
    {
        SynchronizationContexts.Leave();
        return (await Ledger.LoadAsync(DecidedConversation, ct).ConfigureAwait(false)).ToDecisions();
    }

    /// <summary>
    /// Records your app's answers to decisions of the host's conversation, by their ids, and starts no run:
    /// <c>ResumeAsync</c> continues once every decision is answered, and the generated <c>AnswerAsync</c> does both.
    /// A decision may be answered alone, and the same answer again changes nothing.
    /// </summary>
    /// <param name="answers">The answers, at most one for each decision.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The conversation's decisions after the answers.</returns>
    /// <exception cref="DecisionClosedException">
    /// An answer names a decision that is not open; nothing of the call is recorded.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An answer does not fit its decision, one decision is answered twice, or there is no answer; nothing is
    /// recorded.
    /// </exception>
    /// <exception cref="StoreConflictException">Other saves came first ten times in a row.</exception>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="answers"/> is null.</exception>
    public async Task<ConversationDecisions> RecordAnswersAsync(
        IReadOnlyList<DecisionAnswer> answers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(answers);

        SynchronizationContexts.Leave();
        return (await Ledger.RecordAsync(DecidedConversation, answers, ct).ConfigureAwait(false)).ToDecisions();
    }

    /// <summary>
    /// Withdraws what the host's conversation, from <c>WithConversationId</c>, waits for: every decision that is
    /// open, and every answer no run has begun to carry out. None of the calls runs. The conversation's next run,
    /// with new input or from <c>ResumeAsync</c>, tells the model so, and an answer to a withdrawn decision throws
    /// <see cref="DecisionClosedException"/>. Nothing changes when nothing waits. It starts no run.
    /// </summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The conversation's decisions after the withdrawal, none of them open.</returns>
    /// <exception cref="StoreConflictException">Other saves came first ten times in a row.</exception>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    public async Task<ConversationDecisions> CancelDecisionsAsync(CancellationToken ct = default)
    {
        SynchronizationContexts.Leave();
        return (await Ledger.CancelAsync(DecidedConversation, ct).ConfigureAwait(false)).ToDecisions();
    }

    /// <summary>
    /// Switches the mode of the host's conversation, from <c>WithConversationId</c>. Its next run is in
    /// <paramref name="mode"/>, whichever agent answers it, and so are the runs after it, until a run's settings, an
    /// approved plan or this method names another mode. It is the way back from auto mode, and needs no configurable
    /// <c>Mode</c>. It starts no run, and a run going on stays in the mode it began in.
    /// </summary>
    /// <param name="mode">The mode the conversation's next runs are in.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The save.</returns>
    /// <exception cref="StoreConflictException">Other saves came first ten times in a row.</exception>
    /// <exception cref="InvalidOperationException">
    /// The host has no conversation, or stores are registered and none keeps the decisions of its id type.
    /// </exception>
    public async Task SetModeAsync(AgentMode mode, CancellationToken ct = default)
    {
        SynchronizationContexts.Leave();
        await Ledger.SetModeAsync(DecidedConversation, mode, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Withdraws a standing approval of the host's conversation, from <c>WithConversationId</c>: a tool your app
    /// approved for the conversation asks again, from the conversation's next run on. A run going on keeps the
    /// approvals it began with. Nothing changes when the conversation holds no such approval.
    /// </summary>
    /// <param name="tool">The tool's name, as <see cref="StandingApproval.Tool"/> lists it.</param>
    /// <param name="server">
    /// The MCP server that offers the tool, as <see cref="StandingApproval.Server"/> lists it; null for every other
    /// tool.
    /// </param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The save.</returns>
    /// <exception cref="StoreConflictException">Other saves came first ten times in a row.</exception>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="tool"/> is null.</exception>
    public async Task RevokeStandingApprovalAsync(string tool, string? server = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tool);

        SynchronizationContexts.Leave();
        await Ledger.RevokeAsync(DecidedConversation, tool, server, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The raw path in one call: creates the client of a run from the options <c>ResolveOptionsAsync</c> resolves,
    /// for a library that takes an <see cref="IChatClient"/>. The client keeps the options, as
    /// <see cref="CreateChatClientAsync(ChatOptions, CancellationToken)"/> says. The agent's generated
    /// <c>CreateChatClientAsync</c> also takes the run's <c>Settings</c>.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/running-agents#the-raw-path">The raw path</see>.
    /// </summary>
    /// <param name="ct">Cancels the creation.</param>
    /// <returns>The client; disposing it leaves the provider's client alone.</returns>
    /// <exception cref="InvalidOperationException">
    /// No chat client serves the options, the client runs a tool loop of its own, or something the options need is
    /// not registered: a store for the type of the host's settings id, or an <see cref="IMcpToolProvider"/> for the
    /// agent's MCP servers.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">
    /// The stored settings were written under a newer schema version than this code's.
    /// </exception>
    public async Task<IChatClient> CreateChatClientAsync(CancellationToken ct = default) =>
        await CreateChatClientAsync(await ResolveOptionsAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>
    /// The raw path: creates the client of a run whose options <c>ResolveOptionsAsync</c> resolved. For options that
    /// name a provider it takes the <see cref="IChatClient"/> registered under the provider's key, or the client your
    /// <see cref="IChatProviderConnector"/> built for a <see cref="ChatProvider"/> the host was handed. For options
    /// that name none it takes the default provider's. It wraps the client in the tool loop.
    /// The client keeps the options: a call without options runs with them, and a call with options of its own, as a
    /// library passes them, changes only the conversation id, the response format and the tools it adds. A call with
    /// options resolved for another provider or model throws.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/running-agents#the-raw-path">The raw path</see>.
    /// </summary>
    /// <param name="options">The options <c>ResolveOptionsAsync</c> resolved.</param>
    /// <param name="ct">Cancels the creation.</param>
    /// <returns>The client; disposing it leaves the provider's client alone.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <c>ResolveOptionsAsync</c> did not return <paramref name="options"/>, as for <c>ChatOptions</c> your code made.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// No chat client serves the options: they name a provider nothing is registered for, a document while no
    /// <see cref="IChatProviderConnector"/> is registered, or no provider while no default provider is registered. The
    /// default provider never serves a provider the options name. Or they name no model, and the client reports none of
    /// its own; or the run can compact and its summary provider has no client, an agent the run may call or hand off
    /// to names a provider nothing serves, or the client runs a tool loop of its own, as one registered with
    /// <c>UseFunctionInvocation()</c> does.
    /// </exception>
    public async Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        SynchronizationContexts.Leave();

        // D26: the client keeps the options, which hold the agent's provider, run options and settings
        if (options is not ResolvedChatOptions resolved)
        {
            throw new ArgumentException(
                $"The options were not resolved for the agent '{Agent.Name}': create the client from the options "
                + "ResolveOptionsAsync returns, which hold the agent's instructions, tools, provider and run options.",
                nameof(options));
        }

        var provider = resolved.Provider;
        var model = resolved.ModelId;
        var run = resolved.RunOptions;
        var clients = await GetClientsAsync(resolved, ct).ConfigureAwait(false);

        try
        {
            await EnsureServedAsync(resolved, ct).ConfigureAwait(false);
        }
        catch
        {
            clients.Dispose();
            throw;
        }

        var loggers = _services.GetService<ILoggerFactory>();
        var logger = LogEvents.For(loggers);

        // D29: outermost the conversation, then run middleware, context and the mode above the tool loop, the gate
        // (D81), the checkpoints, unique call ids (D59) and compaction inside it, and innermost the model policy's
        // client (D89), which calls each pair of provider and model through its own instrumented client, never
        // disposes a provider's client and ends the run's hold on those Typeshift keeps (D88)
        var provided = new ModelPolicyChatClient(
            run.ModelPolicy,
            clients,
            model,
            _services.GetRequiredService<InstrumentedClients>(),
            Time,
            _services.GetService<TypeshiftOptions>()?.ClassifyModelFailure,
            _services.GetRequiredService<UnknownFailures>(),
            logger);

        var checkpoints = new CheckpointChatClient(new UniqueCallIdsChatClient(new CompactingChatClient(provided)));
        var decisions = new DecisionChatClient(checkpoints);
        var gate = new ToolGate([.. _services.GetServices<IToolMiddleware>()], logger);

        LogEvents.RunResolved(
            logger,
            Agent.Name,
            model,
            provider,
            clients.Of(provider).GetType().Name,
            resolved.Layers);

        var mode = new ModeChatClient(Loop(run, run.MaxToolIterations), Loop);
        var context = new ContextChatClient(mode);

        return new ConversationChatClient(new RunMiddlewareChatClient(context), Agent, _services, resolved);

        // D32: a tool loop per call with that call's run options and what is left of its limit, so concurrent calls
        // never share one
        IChatClient Loop(RunOptions limits, int iterations)
        {
            var loop = new ToolLoop(decisions, loggers, _services)
            {
                MaximumIterationsPerRequest = iterations,
                MaximumConsecutiveErrorsPerRequest = limits.MaxConsecutiveToolErrors,
                AllowConcurrentInvocation = limits.ConcurrentToolCalls,

                // D53: the model fixes a failed call only when it reads why it failed, which the loop passes on for
                // a message written for the model
                IncludeDetailedErrors = true,

                // The call of an answered plan or question resolves whatever the run that continues offers
                AdditionalTools = [ExitPlanModeFunction.Instance, AskUserFunction.Instance],

                // D81: every call goes through its verdict, then the tool middleware (D30)
                FunctionInvoker = gate.InvokeAsync
            };

            return new AuthorNameChatClient(loop, Agent.Name);
        }
    }

    // D89: the clients of every provider the run may use, taken before it stores anything: its own, those its model
    // policy's fallbacks name, and, once it can compact, its summary's (D63), so a fallback nothing serves fails the
    // run at its start and not on the night the first provider is down
    private async Task<RunClients> GetClientsAsync(ResolvedChatOptions resolved, CancellationToken ct)
    {
        var run = resolved.RunOptions;
        var clients = new RunClients(resolved.Provider);

        try
        {
            await AddAsync($"the agent '{Agent.Name}'", resolved.Provider, resolved.ModelId, loops: true)
                .ConfigureAwait(false);

            foreach (var rule in run.ModelPolicy.Rules)
            {
                if (rule.Provider is not { } fallback || clients.Holds(fallback)) continue;

                var policy = $"the model policy of the agent '{Agent.Name}'";
                await AddAsync(policy, fallback, rule.Model ?? resolved.ModelId, loops: true).ConfigureAwait(false);
            }

            // D63: a summary provider nothing serves fails the run here, where it has stored nothing, and not as a
            // warning once the history is compacted
            if (run.MaxInputTokens is not null && resolved.SummaryProvider is { } summary && !clients.Holds(summary))
            {
                var summarized = $"the summary of the agent '{Agent.Name}'";
                await AddAsync(summarized, summary, run.SummaryModel ?? resolved.ModelId, loops: false)
                    .ConfigureAwait(false);
            }

            return clients;
        }
        catch
        {
            clients.Dispose();
            throw;
        }

        async Task AddAsync(string what, ProviderId? provider, string? model, bool loops)
        {
            var (client, hold) = await ChatClients.GetAsync(_services, what, provider, model, ct)
                .ConfigureAwait(false);

            clients.Add(provider, client, hold);

            // D29: a tool loop below Typeshift's own would run tools past the gate, the tool middleware and the run's
            // limits, so the client that brings one is refused before it is called. A summary call offers no tools
            if (loops && client.GetService<FunctionInvokingChatClient>() is not null)
            {
                throw new InvalidOperationException(
                    $"The chat client that serves {what} runs a tool loop of its own, as one registered with "
                    + "UseFunctionInvocation() does. Typeshift runs the tool loop of a run itself, with approval, tool "
                    + "middleware and the run's limits in it: register the client without UseFunctionInvocation().");
            }
        }
    }

    // D87: the providers of the agents the run may call or hand off to are checked before it stores anything too,
    // unless the run was handed one, which serves them all and was found as the run's own. D89: so are those their
    // policies fall back to, unless the run was handed a policy, whose fallbacks were found as the run's own
    private Task EnsureServedAsync(ResolvedChatOptions resolved, CancellationToken ct) =>
        resolved is { HandedProvider: not null, HandedPolicy: not null }
            ? Task.CompletedTask
            : ReachableProviders.EnsureServedAsync(
                Agent,
                _services,
                Store,
                resolved.SettingsId,
                resolved.HandedProvider,
                resolved.HandedPolicy,
                ct);

    /// <summary>
    /// Disposes the scope of a host <see cref="ITypeshiftRuntime.CreateHost{TAgent}"/> created, and with it the scoped
    /// services its runs resolved. A copy from <c>WithSettingsId</c> or <c>WithConversationId</c> disposes the same
    /// scope, so the host and its copies may each be disposed, and once one of them is, a run of any of them throws
    /// an <see cref="ObjectDisposedException"/>. A host your app's container resolved, injected into a request for
    /// example, owns nothing, and disposing it or a copy of it leaves its scope alone.
    /// </summary>
    /// <returns>The disposal.</returns>
    public ValueTask DisposeAsync() => _scope?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <summary>
    /// Disposes the scope of a host <see cref="ITypeshiftRuntime.CreateHost{TAgent}"/> created, as a copy of it does.
    /// Prefer <see cref="DisposeAsync"/>: this one throws when a scoped service disposes only asynchronously.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A service of the scope implements only <see cref="IAsyncDisposable"/>.
    /// </exception>
    public void Dispose() => _scope?.Dispose();

    // The options of the raw path and of the generated members that resolve them, with the host's own call site
    internal Task<ChatOptions> ResolveOptionsCoreAsync(StoreId? settingsId, object? settings, CancellationToken ct) =>
        ResolveOptionsCoreAsync(settingsId, settings, _provider, _policy, ct);

    // D27, D66: the agent's code, its own stored settings, those under the settings id, then the run's; values a layer
    // leaves null keep the one below. D87: over them the call site's provider, which the run may have been handed by
    // the run that called it, and the host's model; D89: and its model policy, handed on the same way. D63: a run
    // that names no provider then takes the default one
    private async Task<ChatOptions> ResolveOptionsCoreAsync(
        StoreId? settingsId,
        object? settings,
        ProviderId? handed,
        ModelPolicy? policy,
        CancellationToken ct)
    {
        SynchronizationContexts.Leave();
        var declaration = Agent.GeneratedDeclaration;
        var loaded = await declaration.LoadLayersAsync(Store, settingsId, ct).ConfigureAwait(false);
        IReadOnlyList<object> layers = [.. loaded.Append(settings).OfType<object>()];

        var options = new ResolvedChatOptions(Agent.ChatOptions)
        {
            Instructions = Agent.InstructionsFor(_services),
            Provider = Agent.Provider
        };
        var run = Agent.RunOptions;

        foreach (var layer in layers)
        {
            run = declaration.ApplySettings(options, run, layer);
        }

        if (handed is not null)
        {
            options.Provider = handed;
            options.HandedProvider = handed;
            options.SummaryProvider = null;
        }

        if (policy is not null)
        {
            run = run with { ModelPolicy = policy };
            options.HandedPolicy = policy;
        }

        if (_model is not null) options.ModelId = _model;
        options.Provider ??= ChatClients.DefaultProvider(_services);

        // D63, D88: a document's model answers a run that names none
        if (options is { ModelId: null, Provider: ProviderDocument { Provider.Model: { } model } })
            options.ModelId = model;

        string?[] applied =
        [
            "its code",
            loaded[0] is null ? null : "its stored settings",
            loaded[1] is null ? null : $"settings id {settingsId}",
            settings is null ? null : "the run",
            handed is null && _model is null && policy is null ? null : "the call site"
        ];

        options.Layers = string.Join(", ", applied.OfType<string>());
        options.RunOptions = run;

        // D32: what a layer set that no run can go with fails here, by its name, before anything runs
        if (options.GetInvalid() is { } invalid)
        {
            throw new InvalidOperationException(
                $"The agent '{Agent.Name}' resolved {invalid.Option} {invalid.Value} from the settings of "
                + $"{options.Layers}: it must {invalid.Must}.");
        }

        var tools = declaration.CreateTools(Agent, layers, _services.GetRequiredService<ToolJson>());

        // D33, D35: the sub-agents and the MCP servers' tools after the toolsets'
        var agents = Agent.SubAgents.Select(use => new SubAgentFunction(use, use.Host(_services).Agent));
        List<AITool> own = [.. tools, .. agents, .. options.Tools ?? []];

        options.Tools = [.. own, .. await McpToolsAsync(own, ct).ConfigureAwait(false)];
        options.RunSetsMode = NamedMode(settings) is not null;
        options.SettingsId = settingsId;

        // The raw path's conversation, which a run replaces with its own
        options.Conversation = _conversationId;

        return options;
    }

    // A run without input resumes the conversation's interrupted run (D50)
    internal Task<ChatResponse> RunCoreAsync(
        ChatMessage? input,
        object? settings,
        CancellationToken ct,
        bool retries = false) =>
        RunAsync(input is null ? [] : [input], OwnRun(Conversation(input), retries: retries), settings, ct);

    // A resume needs a conversation
    private StoreId? Conversation(ChatMessage? input) => input is null ? ResumedConversation : _conversationId;

    // A run the host's own members start is handed nothing but what the host was given: its ids, its provider and
    // its model policy
    private RunHandover OwnRun(StoreId? conversation, ChatResponseFormat? format = null, bool retries = false) =>
        new(_settingsId, conversation, Claim: null, format, Retries: retries, Provider: _provider, Policy: _policy);

    internal async Task<ChatResponse<TResult>> RunCoreAsync<TResult>(
        ChatMessage? input,
        object? settings,
        CancellationToken ct,
        bool retries = false)
    {
        var result = _services.GetRequiredService<ToolJson>().GetResultFormat<TResult>();
        var response = await RunAsync(
            input is null ? [] : [input],
            OwnRun(Conversation(input), result.Format, retries),
            settings,
            ct).ConfigureAwait(false);

        return new ChatResponse<TResult>(response, result.SerializerOptions);
    }

    // D64: the typed answer's format, as RunCoreAsync<TResult> asks for it, over a streaming run; one without input
    // resumes the conversation
    internal StreamingRun<TResult> RunStreamingCoreAsync<TResult>(
        ChatMessage? input,
        object? settings,
        CancellationToken ct,
        bool retries = false)
    {
        var result = _services.GetRequiredService<ToolJson>().GetResultFormat<TResult>();
        var handover = OwnRun(Conversation(input), result.Format, retries);

        return new StreamingRun<TResult>(
            RunStreamingAsync(input is null ? [] : [input], handover, settings, ct),
            result.SerializerOptions);
    }

    // D80: the answers are recorded first, by compare-and-set; the run then continues as a resume does, which calls
    // no model while a decision is left open
    internal async Task<ChatResponse> AnswerCoreAsync(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
    {
        await RecordAnswersAsync(answers, ct).ConfigureAwait(false);
        return await RunCoreAsync(null, settings, ct).ConfigureAwait(false);
    }

    internal async Task<ChatResponse<TResult>> AnswerCoreAsync<TResult>(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
    {
        await RecordAnswersAsync(answers, ct).ConfigureAwait(false);
        return await RunCoreAsync<TResult>(null, settings, ct).ConfigureAwait(false);
    }

    internal StreamingRun AnswerStreamingCoreAsync(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct) =>
        new(AnswerStreamingAsync(answers, settings, null, ct));

    internal StreamingRun<TResult> AnswerStreamingCoreAsync<TResult>(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
    {
        var result = _services.GetRequiredService<ToolJson>().GetResultFormat<TResult>();

        return new StreamingRun<TResult>(
            AnswerStreamingAsync(answers, settings, result.Format, ct),
            result.SerializerOptions);
    }

    private async IAsyncEnumerable<ChatResponseUpdate> AnswerStreamingAsync(
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        ChatResponseFormat? format,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await RecordAnswersAsync(answers, ct).ConfigureAwait(false);

        var resumed = RunStreamingAsync([], OwnRun(ResumedConversation, format), settings, ct);

        await foreach (var update in resumed.ConfigureAwait(false))
        {
            yield return update;
        }
    }

    internal StreamingRun RunStreamingCoreAsync(
        ChatMessage? input,
        object? settings,
        CancellationToken ct,
        bool retries = false)
    {
        var handover = OwnRun(Conversation(input), retries: retries);
        return new(RunStreamingAsync(input is null ? [] : [input], handover, settings, ct));
    }

    internal StoreAccess Store => _services.GetRequiredService<StoreAccess>();

    Agent IAgentHost.Agent => Agent;

    // D34: another agent's run applies none of the run settings of the run that handed it over
    Task<ChatResponse> IAgentHost.RunAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        CancellationToken ct) =>
        RunAsync(messages, handover, settings: null, ct);

    IAsyncEnumerable<ChatResponseUpdate> IAgentHost.RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        CancellationToken ct) =>
        RunStreamingAsync(messages, handover, settings: null, ct);

    // D34: the run takes its conversation before anything resolves, so on a conversation another agent answers,
    // this agent loads no settings, lists no MCP server and asks for no chat client. The claim is the run's once it
    // began, and free again when it never did
    private async Task<ChatResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        object? settings,
        CancellationToken ct)
    {
        SynchronizationContexts.Leave();
        var (taken, answering) = await Start.TakeAsync(handover, NamedMode(settings), ct).ConfigureAwait(false);

        try
        {
            if (answering is not null) return await answering.RunAsync(messages, taken, ct).ConfigureAwait(false);

            var options = await ResolveRunAsync(taken, settings, ct).ConfigureAwait(false);
            using var client = await CreateChatClientAsync(options, ct).ConfigureAwait(false);

            return await client.GetResponseAsync(messages, options, ct).ConfigureAwait(false);
        }
        finally
        {
            Runs.Release(taken.Claim);
        }
    }

    private async IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        object? settings,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (taken, answering) = await Start.TakeAsync(handover, NamedMode(settings), ct).ConfigureAwait(false);

        try
        {
            if (answering is not null)
            {
                await foreach (var update in answering.RunStreamingAsync(messages, taken, ct).ConfigureAwait(false))
                {
                    yield return update;
                }

                yield break;
            }

            var options = await ResolveRunAsync(taken, settings, ct).ConfigureAwait(false);
            using var client = await CreateChatClientAsync(options, ct).ConfigureAwait(false);

            await foreach (var update in client.GetStreamingResponseAsync(messages, options, ct).ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            Runs.Release(taken.Claim);
        }
    }

    private RunStart Start => new(Agent, _services, Ledger);

    private LiveRuns Runs => _services.GetRequiredService<LiveRuns>();

    // D35: a server's tool named like one the agent offers would leave the model one of them, unseen
    private async Task<List<AITool>> McpToolsAsync(IEnumerable<AITool> own, CancellationToken ct)
    {
        if (Agent.McpServers.Count == 0) return [];

        var provider = _services.GetService<IMcpToolProvider>()
            ?? throw new InvalidOperationException(
                $"The agent '{Agent.Name}' uses MCP servers, and no IMcpToolProvider is registered: call "
                + "WithMcpToolProvider<T>() after AddTypeshift().");

        var names = own.Select(tool => tool.Name).ToHashSet();
        var offered = new List<AITool>();

        foreach (var use in Agent.McpServers)
        {
            foreach (var tool in await provider.GetToolsAsync(use.Server, ct).ConfigureAwait(false))
            {
                if (!names.Add(tool.Name))
                {
                    throw new InvalidOperationException(
                        $"The MCP server '{use.Server}' offers a tool '{tool.Name}', which the agent '{Agent.Name}' "
                        + "already offers: rename one of them, or leave the server out.");
                }

                offered.Add(use.Offer(tool));
            }
        }

        return offered;
    }

    // D52: the mode the run's own settings name. A layer names one when it moves two different modes to the same
    private AgentMode? NamedMode(object? settings)
    {
        if (settings is null) return null;

        var declaration = Agent.GeneratedDeclaration;
        var named = declaration.ApplySettings(new ChatOptions(), new RunOptions(), settings).Mode;
        var onPlan = declaration.ApplySettings(new ChatOptions(), new RunOptions { Mode = AgentMode.Plan }, settings);

        return named == onPlan.Mode ? named : null;
    }

    // The run's options with what the run was handed: the conversation and the claim on it (D34, D50), a typed run's
    // format, which an agent handed the run or answering in its place keeps (D64), and a sub-agent's caller, in
    // whose mode its run goes (D33)
    private async Task<ResolvedChatOptions> ResolveRunAsync(
        RunHandover handover,
        object? settings,
        CancellationToken ct)
    {
        var options = (ResolvedChatOptions)await ResolveOptionsCoreAsync(
            handover.SettingsId,
            settings,
            handover.Provider,
            handover.Policy,
            ct).ConfigureAwait(false);
        options.Conversation = handover.Conversation;
        options.Claim = handover.Claim;
        options.Handoffs = handover.Handoffs;
        options.Entries = handover.Entries;
        options.HostRun = true;
        options.Retries = handover.Retries;

        if (handover.Format is { } format)
        {
            options.ResponseFormat = format;
            options.ResultFormat = format;
        }

        if (handover.Caller is { } caller)
        {
            options.RunOptions = options.RunOptions with { Mode = caller.Mode };
            options.Caller = caller;
        }

        return options;
    }
}
