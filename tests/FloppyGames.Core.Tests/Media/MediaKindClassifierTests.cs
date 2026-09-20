using FloppyGames.Core.Media;

namespace FloppyGames.Core.Tests.Media;

public class MediaKindClassifierTests
{
    [Theory]
    [InlineData("A:\\")]
    [InlineData("B:\\")]
    [InlineData("a:\\")]
    public void Classify_FloppyCandidateLetters_ReturnsFloppy(string driveRoot)
    {
        Assert.Equal(MediaKind.Floppy, MediaKindClassifier.Classify(driveRoot));
    }

    [Theory]
    [InlineData("E:\\")]
    [InlineData("F:\\")]
    [InlineData("D:\\")]
    public void Classify_OtherLetters_ReturnsUsb(string driveRoot)
    {
        Assert.Equal(MediaKind.Usb, MediaKindClassifier.Classify(driveRoot));
    }
}
