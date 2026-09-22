using FloppyGames.Core.Nfc;

namespace FloppyGames.Core.Tests.Nfc;

public class NfcCardPasswordKeyTests
{
    [Fact]
    public void Derive_ReturnsSixBytes()
    {
        Assert.Equal(6, NfcCardPasswordKey.Derive("hunter2").Length);
    }

    [Fact]
    public void Derive_SamePassword_IsDeterministic()
    {
        Assert.Equal(NfcCardPasswordKey.Derive("hunter2"), NfcCardPasswordKey.Derive("hunter2"));
    }

    [Fact]
    public void Derive_DifferentPasswords_ProduceDifferentKeys()
    {
        Assert.NotEqual(NfcCardPasswordKey.Derive("hunter2"), NfcCardPasswordKey.Derive("hunter3"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Derive_BlankPassword_Throws(string password)
    {
        Assert.Throws<ArgumentException>(() => NfcCardPasswordKey.Derive(password));
    }
}
