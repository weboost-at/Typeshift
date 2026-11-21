#pragma warning disable TYPESHIFT001 // the model policy is experimental, and these tests pin its value
using Typeshift.Tests.Hosting.Support;
using FluentAssertions;

namespace Typeshift.Tests.Hosting;

// D89: a model policy is a value, equal to another with the same rules in the same order and the same timeout
public class ModelPolicyTests : UnitTestBase
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan LongPatience = TimeSpan.FromMinutes(2);

    [Fact]
    public void Given_The_Same_Rules_Two_Policies_Are_Equal()
    {
        // Arrange
        var first = Policy();

        // Act
        var second = Policy();

        // Assert
        second.Should().Be(first);
    }

    [Fact]
    public void Given_The_Same_Rules_Two_Policies_Have_The_Same_Hash()
    {
        // Arrange
        var first = Policy();

        // Act
        var second = Policy();

        // Assert
        second.GetHashCode().Should().Be(first.GetHashCode());
    }

    [Fact]
    public void Given_The_Same_Rules_In_Another_Order_Two_Policies_Differ()
    {
        // Arrange
        var first = ModelPolicy.Handle(ModelFailure.RateLimit).Retry(1).Or(ModelFailure.Auth).Retry(2);

        // Act
        var second = ModelPolicy.Handle(ModelFailure.Auth).Retry(2).Or(ModelFailure.RateLimit).Retry(1);

        // Assert
        second.Should().NotBe(first);
    }

    [Fact]
    public void Given_A_Timeout_It_Replaces_The_Policys_Own()
    {
        // Arrange
        var policy = Policy().Timeout(Patience);

        // Act
        var patient = policy.Timeout(LongPatience);

        // Assert
        patient.Should().Be(Policy().Timeout(LongPatience));
    }

    [Fact]
    public void Given_A_Timeout_The_Policy_Keeps_Its_Rules()
    {
        // Act
        var policy = Policy().Timeout(Patience);

        // Assert
        policy.Rules.Should().Equal(Policy().Rules);
    }

    [Fact]
    public void Given_No_Rules_And_No_Timeout_A_Policy_Is_None()
    {
        // Act
        var policy = ModelPolicy.None.Timeout(Patience).Timeout(Timeout.InfiniteTimeSpan);

        // Assert
        policy.Should().Be(ModelPolicy.None);
    }

    [Fact]
    public void Given_A_Policy_Its_Text_Names_Its_Rules_As_Code_Writes_Them()
    {
        // Act
        var text = Policy().Timeout(Patience).ToString();

        // Assert
        text.Should().Be(
            "Handle(Transient).Retry(2, 00:00:01, 1.5).Or(Rejected).FallbackToModel('fallback-model')"
            + ".Or(Auth, Unknown).FallbackToProvider('Other').Timeout(00:00:30)");
    }

    [Fact]
    public void Given_A_Document_A_Fallback_Takes_It_As_The_Document_It_Is()
    {
        // Arrange
        var document = new CustomerProvider(Data.Agent.ConversationId) { Kind = Data.Agent.Provider };

        // Act
        var policy = ModelPolicy.Handle(ModelFailure.Any).FallbackToProvider(document);

        // Assert
        policy.Rules[0].Provider.Should().Be(new ProviderDocument(document));
    }

    [Fact]
    public void Given_A_Document_The_Policys_Text_Leaves_Its_Key_Out()
    {
        // Arrange
        var document = new ChatProvider { Kind = Data.Agent.Provider, ApiKey = Data.Agent.Instructions };

        // Act
        var text = ModelPolicy.Handle(ModelFailure.Any).FallbackTo(document, Data.Agent.Model).ToString();

        // Assert
        text.Should().Be($"Handle(Any).FallbackTo('{Data.Agent.Provider}', '{Data.Agent.Model}')");
    }

    [Fact]
    public void Given_A_Retry_Its_Waits_Grow_By_The_Backoff()
    {
        // Arrange
        var rule = ModelPolicy.Handle(ModelFailure.Transient).Retry(3, TimeSpan.FromSeconds(1), backoff: 3).Rules[0];

        // Act
        var waits = Enumerable.Range(1, 3).Select(rule.DelayBefore);

        // Assert
        waits.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(9));
    }

    [Fact]
    public void Given_A_Rule_Of_No_Failure_It_Is_Refused()
    {
        // Act
        var handle = () => ModelPolicy.Handle(ModelFailure.None);

        // Assert
        handle.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("failures");
    }

    [Fact]
    public void Given_A_Retry_Of_No_Try_It_Is_Refused()
    {
        // Act
        var retry = () => ModelPolicy.Handle(ModelFailure.Transient).Retry(0);

        // Assert
        retry.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("times");
    }

    [Fact]
    public void Given_A_Backoff_Below_One_It_Is_Refused()
    {
        // Act
        var retry = () => ModelPolicy.Handle(ModelFailure.Transient).Retry(1, TimeSpan.FromSeconds(1), backoff: 0.5);

        // Assert
        retry.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("backoff");
    }

    [Fact]
    public void Given_A_Timeout_Of_Zero_It_Is_Refused()
    {
        // Act
        var timeout = () => ModelPolicy.None.Timeout(TimeSpan.Zero);

        // Assert
        timeout.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("untilFirstUpdate");
    }

    [Fact]
    public void Given_A_Blank_Model_A_Fallback_Is_Refused()
    {
        // Act
        var fallback = () => ModelPolicy.Handle(ModelFailure.Rejected).FallbackToModel(" ");

        // Assert
        fallback.Should().Throw<ArgumentException>().WithParameterName("model");
    }

    [Fact]
    public void Given_A_Blank_Key_A_Fallback_Is_Refused()
    {
        // Act
        var fallback = () => ModelPolicy.Handle(ModelFailure.Unavailable).FallbackToProvider(" ");

        // Assert
        fallback.Should().Throw<ArgumentException>().WithParameterName("provider");
    }

    private static ModelPolicy Policy() =>
        ModelPolicy
            .Handle(ModelFailure.Transient).Retry(2, TimeSpan.FromSeconds(1), backoff: 1.5)
            .Or(ModelFailure.Rejected).FallbackToModel("fallback-model")
            .Or(ModelFailure.Auth | ModelFailure.Unknown).FallbackToProvider(Vendor.Other);
}

#pragma warning restore TYPESHIFT001
