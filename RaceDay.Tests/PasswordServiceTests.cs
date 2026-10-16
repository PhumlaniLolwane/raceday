using RaceDay.Api.Services;

namespace RaceDay.Tests;

public class PasswordServiceTests
{
    private readonly PasswordService _svc = new();

    [Fact]
    public void Hash_IsNotThePlainPassword()
    {
        var hash = _svc.Hash("Password123!");
        Assert.NotEqual("Password123!", hash);
        Assert.DoesNotContain("Password123!", hash);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue() =>
        Assert.True(_svc.Verify("Password123!", _svc.Hash("Password123!")));

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse() =>
        Assert.False(_svc.Verify("wrong", _svc.Hash("Password123!")));

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashes() =>
        Assert.NotEqual(_svc.Hash("Password123!"), _svc.Hash("Password123!"));

    [Fact]
    public void Verify_MalformedHash_ReturnsFalse() =>
        Assert.False(_svc.Verify("Password123!", "HASHED_PASSWORD_1"));
}
