namespace Prism.Services.Upscale;

// Routes all upscale calls to the Real-ESRGAN model (Real-ESRGAN ×2 + Lanczos4 top-up; DirectML when
// a hardware adapter is present, CPU otherwise — OnnxSessionFactory decides) whenever its session
// loaded, or the Lanczos4 fallback (capped ×1.42) when the model asset is unavailable.
public static class ImageUpscaler {
    private static readonly bool GpuAvailable = GpuProbe.HasHardwareDirectMLAdapter();

    /// <summary>True when a hardware DirectML adapter was detected at startup.</summary>
    public static bool IsGpuAvailable => GpuAvailable;

    public static byte[] Upscale( byte[] imageBytes, double scaleFactor ) =>
        Upscaler_g_p_u.IsReady
            ? Upscaler_g_p_u.Upscale(imageBytes, scaleFactor)
            : Upscaler_c_p_u.Upscale(imageBytes, scaleFactor);
}
