using project.Services;

namespace AttendanceSystem.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesPrefixedValue_WithFourParts()
    {
        var hash = PasswordHasher.Hash("secret123");

        Assert.StartsWith("pbkdf2$", hash);
        Assert.Equal(4, hash.Split('$').Length);
        Assert.True(hash.Length <= 255, "hash must fit in VARCHAR(255)");
    }

    [Fact]
    public void Hash_UsesRandomSalt()
    {
        Assert.NotEqual(PasswordHasher.Hash("same"), PasswordHasher.Hash("same"));
    }

    [Fact]
    public void Verify_AcceptsCorrectPassword()
    {
        var hash = PasswordHasher.Hash("correct horse");
        Assert.True(PasswordHasher.Verify("correct horse", hash));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hash = PasswordHasher.Hash("correct horse");
        Assert.False(PasswordHasher.Verify("battery staple", hash));
    }

    [Theory]
    [InlineData("plain123", "plain123", true)]
    [InlineData("plain123", "Plain123", false)]
    [InlineData("plain123", "", false)]
    public void Verify_SupportsLegacyPlainText(string stored, string attempt, bool expected)
    {
        Assert.Equal(expected, PasswordHasher.Verify(attempt, stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pbkdf2$notanumber$abc$def")]
    [InlineData("pbkdf2$1000$!!notbase64!!$abc")]
    [InlineData("pbkdf2$1000$onlythree")]
    public void Verify_RejectsMalformedStoredValues(string? stored)
    {
        Assert.False(PasswordHasher.Verify("anything", stored));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForNullPassword()
    {
        Assert.False(PasswordHasher.Verify(null, PasswordHasher.Hash("x")));
    }

    [Fact]
    public void NeedsRehash_TrueForPlainText_FalseForCurrentHash()
    {
        Assert.True(PasswordHasher.NeedsRehash("plain"));
        Assert.True(PasswordHasher.NeedsRehash(PasswordHasher.Hash("x", iterations: 1000)));
        Assert.False(PasswordHasher.NeedsRehash(PasswordHasher.Hash("x")));
    }
}
