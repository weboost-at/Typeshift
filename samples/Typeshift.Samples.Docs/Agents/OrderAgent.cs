using Typeshift.Samples.Orders;

namespace Typeshift.Samples.Agents;

// Runs the calls of one response at the same time, so its tools read the database through a factory
internal partial class OrderAgent : Agent
{
    public OrderAgent()
    {
        WithDescription("Answers questions about the customer's orders.");
        WithInstructions("Look up each order the customer names, and say where it is.");

        WithChatOptions()
            .WithProvider("openai")
            .WithModel("gpt-5-mini");

        UseToolset<OrderToolset>();

        WithRunOptions()
            .WithConcurrentToolCalls(true);
    }
}
