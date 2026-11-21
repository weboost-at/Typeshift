#pragma warning disable TYPESHIFT001 // the model policy is experimental, and these tests pin its failure kinds
using System.Net;
using System.Net.Sockets;
using FluentAssertions;

namespace Typeshift.Tests.Hosting;

// D89: which kind a model call's failure is, by .NET's own exceptions, with your app's classification asked first
public class ModelFailureTests : UnitTestBase
{
    [Theory]
    [InlineData(429, ModelFailure.RateLimit)]
    [InlineData(408, ModelFailure.Timeout)]
    [InlineData(504, ModelFailure.Timeout)]
    [InlineData(401, ModelFailure.Auth)]
    [InlineData(403, ModelFailure.Auth)]
    [InlineData(400, ModelFailure.Rejected)]
    [InlineData(404, ModelFailure.Rejected)]
    [InlineData(413, ModelFailure.Rejected)]
    [InlineData(422, ModelFailure.Rejected)]
    [InlineData(500, ModelFailure.Unavailable)]
    [InlineData(503, ModelFailure.Unavailable)]
    [InlineData(418, ModelFailure.Unknown)]
    public void Given_A_Status_Its_Kind_Is_The_Tables(int status, ModelFailure kind)
    {
        // Act
        var failure = ModelFailure.FromStatus(status);

        // Assert
        failure.Should().Be(kind);
    }

    [Fact]
    public void Given_A_Request_That_Failed_With_A_Status_Its_Kind_Is_The_Statuss()
    {
        // Arrange
        var exception = new HttpRequestException(Data.Agent.Reason, null, HttpStatusCode.TooManyRequests);

        // Act
        var failure = ModelFailures.Classify(exception, classify: null);

        // Assert
        failure.Should().Be(ModelFailure.RateLimit);
    }

    [Fact]
    public void Given_A_Request_That_Got_No_Status_The_Provider_Is_Unavailable()
    {
        // Act
        var failure = ModelFailures.Classify(new HttpRequestException(Data.Agent.Reason), classify: null);

        // Assert
        failure.Should().Be(ModelFailure.Unavailable);
    }

    [Fact]
    public void Given_A_Socket_That_Failed_The_Provider_Is_Unavailable()
    {
        // Act
        var failure = ModelFailures.Classify(new SocketException(), classify: null);

        // Assert
        failure.Should().Be(ModelFailure.Unavailable);
    }

    [Fact]
    public void Given_A_Stream_That_Failed_The_Provider_Is_Unavailable()
    {
        // Act
        var failure = ModelFailures.Classify(new IOException(Data.Agent.Reason), classify: null);

        // Assert
        failure.Should().Be(ModelFailure.Unavailable);
    }

    [Fact]
    public void Given_A_Timeout_Exception_The_Call_Timed_Out()
    {
        // Act
        var failure = ModelFailures.Classify(new TimeoutException(Data.Agent.Reason), classify: null);

        // Assert
        failure.Should().Be(ModelFailure.Timeout);
    }

    [Fact]
    public void Given_A_Cancellation_The_Call_Timed_Out()
    {
        // Act
        var failure = ModelFailures.Classify(new TaskCanceledException(Data.Agent.Reason), classify: null);

        // Assert
        failure.Should().Be(ModelFailure.Timeout);
    }

    [Fact]
    public void Given_An_Exception_Of_A_Client_Library_It_Is_Unknown()
    {
        // Act
        var failure = ModelFailures.Classify(new InvalidOperationException(Data.Agent.Reason), classify: null);

        // Assert
        failure.Should().Be(ModelFailure.Unknown);
    }

    [Fact]
    public void Given_The_Apps_Classification_It_Answers_Before_The_Table()
    {
        // Arrange
        var exception = new HttpRequestException(Data.Agent.Reason, null, HttpStatusCode.ServiceUnavailable);

        // Act
        var failure = ModelFailures.Classify(exception, _ => ModelFailure.ContentFilter);

        // Assert
        failure.Should().Be(ModelFailure.ContentFilter);
    }

    [Fact]
    public void Given_The_Apps_Classification_Leaves_An_Exception_The_Table_Answers()
    {
        // Act
        var failure = ModelFailures.Classify(new IOException(Data.Agent.Reason), _ => null);

        // Assert
        failure.Should().Be(ModelFailure.Unavailable);
    }
}
