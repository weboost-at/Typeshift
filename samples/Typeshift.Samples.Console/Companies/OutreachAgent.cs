using Typeshift.Samples.Chat;

namespace Typeshift.Samples.Companies;

// Asks the user which company to write to, and sends the mail once the app approved it: the two decisions of the
// Native AOT build
internal partial class OutreachAgent : Agent
{
    public OutreachAgent()
    {
        WithChatOptions()
            .WithModel(Provider.Scripted, "scripted-model");

        AddTool(SendMail)
            .RequiresApproval();

        UseQuestions();
    }

    /// <summary>
    /// Sends a mail to a company.
    /// </summary>
    /// <param name="company">The company's name.</param>
    private static string SendMail(string company) => $"Sent to {company}.";
}
