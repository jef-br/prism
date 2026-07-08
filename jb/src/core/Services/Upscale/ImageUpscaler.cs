namespace Prism.Services.Upscale;

// Probes for a hardware DirectML adapter once at startup, then routes all upscale calls to
// the GPU strategy (Real-ESRGAN ×2 + Lanczos4) or the CPU fallback (Lanczos4, capped ×1.42).
public static class ImageUpscaler {
    private static readonly bool GpuAvailable = GpuProbe.HasHardwareDirectMLAdapter();

    /// <summary>True when a hardware DirectML adapter was detected at startup.</summary>
    public static bool IsGpuAvailable => GpuAvailable;

    public static byte[] Upscale( byte[] imageBytes, double scaleFactor ) =>
        GpuAvailable && Upscaler_g_p_u.IsReady
            ? Upscaler_g_p_u.Upscale(imageBytes, scaleFactor)
            : Upscaler_c_p_u.Upscale(imageBytes, scaleFactor);
}
