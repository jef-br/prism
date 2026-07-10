using Xunit;

namespace PrismCoreTests.Upscale;

/// <summary>
/// Unit tests for Upscaler_g_p_u's non-throwing, idempotent Initialize contract (T-2800).
/// Upscaler_g_p_u holds one process-wide static ONNX session, so these tests deliberately avoid
/// asserting an absolute IsReady value: PipelineIntegrationTests constructs real PrismService instances
/// in the same test process and may already have loaded a real session before these run, and xUnit does
/// not guarantee cross-class ordering. Every assertion below is either "never throws" or relative to
/// whatever IsReady was immediately before the call, so they hold regardless of run order.
/// </summary>
public class Upscaler_g_p_uTests
{
    private const string NonexistentModelPath = @"Z:\definitely\does\not\exist\Real-ESRGAN_x2plus.onnx";
    private const string NonexistentConfigPath = @"Z:\definitely\does\not\exist\cfg_Upscale.json";

    [Fact]
    public void Initialize_NonexistentPath_DoesNotThrow()
    {
        Exception? exception = Record.Exception(() => Upscaler_g_p_u.Initialize(NonexistentModelPath, NonexistentConfigPath));

        Assert.Null(exception);
    }

    [Fact]
    public void Initialize_NonexistentPath_DoesNotChangeReadiness()
    {
        bool before = Upscaler_g_p_u.IsReady;

        Upscaler_g_p_u.Initialize(NonexistentModelPath, NonexistentConfigPath);

        Assert.Equal(before, Upscaler_g_p_u.IsReady);
    }

    [Fact]
    public void Initialize_CalledTwiceWithNonexistentPath_IsIdempotentAndDoesNotThrow()
    {
        Exception? firstException = Record.Exception(() => Upscaler_g_p_u.Initialize(NonexistentModelPath, NonexistentConfigPath));
        bool afterFirst = Upscaler_g_p_u.IsReady;

        Exception? secondException = Record.Exception(() => Upscaler_g_p_u.Initialize(NonexistentModelPath, NonexistentConfigPath));

        Assert.Null(firstException);
        Assert.Null(secondException);
        Assert.Equal(afterFirst, Upscaler_g_p_u.IsReady);
    }

    [Fact]
    public async Task Initialize_ConcurrentCallsWithNonexistentPath_DoNotDeadlock()
    {
        Task[] tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => Upscaler_g_p_u.Initialize(NonexistentModelPath, NonexistentConfigPath)))
            .ToArray();

        Task allTasks = Task.WhenAll(tasks);
        Task completedTask = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(allTasks, completedTask);
    }
}
