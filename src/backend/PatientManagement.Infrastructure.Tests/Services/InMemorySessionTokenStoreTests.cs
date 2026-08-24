using PatientManagement.Application.Services;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

public class InMemorySessionTokenStoreTests
{
    [Fact]
    public void Token_issued_for_a_username_resolves_back_to_that_username()
    {
        ISessionTokenStore store = new InMemorySessionTokenStore();

        var token = store.IssueToken("doctor");

        Assert.True(store.TryGetUsername(token, out var username));
        Assert.Equal("doctor", username);
    }

    [Fact]
    public void Unknown_token_does_not_resolve()
    {
        ISessionTokenStore store = new InMemorySessionTokenStore();

        Assert.False(store.TryGetUsername("never-issued", out _));
    }

    [Fact]
    public void Two_tokens_issued_for_the_same_username_are_different()
    {
        ISessionTokenStore store = new InMemorySessionTokenStore();

        var first = store.IssueToken("doctor");
        var second = store.IssueToken("doctor");

        Assert.NotEqual(first, second);
    }
}
