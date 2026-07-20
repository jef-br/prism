using Prism.Core;
using Prism.Services.Matching;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Unit tests for ImageClassifier's Initialize load contract (T-4110): a missing model file degrades
/// quietly to IsReady = false (existence is validated loud upstream by
/// ClassificationService.ResolveClassifierPaths), while a present-but-corrupt file must throw
/// PrismConfigurationException — corrupt assets never degrade silently. Uses fresh instances, never
/// GetShared, so the process-wide shared classifier is not touched.
/// </summary>
public class ImageClassifierTests
{
    private static readonly string NonexistentPath = Path.Combine(Path.GetTempPath(), "does-not-exist.onnx");

    [Fact]
    public void Initialize_WithMissingModelFile_DoesNotThrowAndIsNotReady()
    {
        using var classifier = new ImageClassifier();

        classifier.Initialize(NonexistentPath, NonexistentPath, NonexistentPath);

        Assert.False(classifier.IsReady);
    }

    [Fact]
    public void Initialize_WithCorruptModelFile_ThrowsPrismConfigurationException()
    {
        string corruptPath = Path.Combine(Path.GetTempPath(), $"corrupt-clip-{Guid.NewGuid():N}.onnx");
        File.WriteAllBytes(corruptPath, [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03]);

        try
        {
            using var classifier = new ImageClassifier();
            PrismConfigurationException exception = Assert.Throws<PrismConfigurationException>(
                () => classifier.Initialize(corruptPath, NonexistentPath, NonexistentPath));
            Assert.Contains("corrupt", exception.Message);
            Assert.False(classifier.IsReady);
        }
        finally
        {
            File.Delete(corruptPath);
        }
    }
}
