using System.Runtime.InteropServices;

namespace Prism.Services.Matching;

// Probes DXGI for a hardware DX12-capable adapter (i.e. not Microsoft WARP software renderer).
// Hardware adapter present → OnnxSessionFactory appends the DirectML EP → models run on GPU.
// All-software or no DX12 → sessions run on the CPU execution provider.
internal static class GpuProbe {
    // IDXGIFactory1 {770aae78-f26f-4dba-a829-253c83d1b387}
    private static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    private const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;

    // vtable offsets — IDXGIFactory1 chain: IUnknown(0-2) + IDXGIObject(3-6) + IDXGIFactory(7-11) + IDXGIFactory1(12+)
    private const int VtableRelease        = 2;   // IUnknown::Release
    private const int VtableEnumAdapters1  = 12;  // IDXGIFactory1::EnumAdapters1
    // IDXGIAdapter1 chain: IUnknown(0-2) + IDXGIObject(3-6) + IDXGIAdapter(7-9) + IDXGIAdapter1(10+)
    private const int VtableGetDesc1       = 10;  // IDXGIAdapter1::GetDesc1

    [DllImport("dxgi.dll", CallingConvention = CallingConvention.Winapi, PreserveSig = true)]
    private static extern int CreateDXGIFactory1(in Guid riid, out nint ppFactory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1 {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId, DeviceId, SubSysId, Revision;
        public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int  EnumAdapters1Fn(nint f, uint i, out nint pp);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int  GetDesc1Fn(nint a, ref DXGI_ADAPTER_DESC1 d);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint ReleaseFn(nint p);

    internal static bool HasHardwareDirectMLAdapter() {
        if (!OperatingSystem.IsWindows()) return false;
        try {
            var iid = IID_IDXGIFactory1;
            if (CreateDXGIFactory1(in iid, out nint factory) < 0) return false;
            try {
                var vtF  = Marshal.ReadIntPtr(factory);
                var enumAdapters1 = Fn<EnumAdapters1Fn>(vtF, VtableEnumAdapters1);
                bool found = false;
                for (uint i = 0; !found; i++) {
                    if (enumAdapters1(factory, i, out nint adapter) < 0) break;
                    try {
                        var vtA  = Marshal.ReadIntPtr(adapter);
                        DXGI_ADAPTER_DESC1 desc = default;
                        if (Fn<GetDesc1Fn>(vtA, VtableGetDesc1)(adapter, ref desc) >= 0
                            && (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) == 0)
                            found = true;
                    } finally { Fn<ReleaseFn>(Marshal.ReadIntPtr(adapter), VtableRelease)(adapter); }
                }
                return found;
            } finally { Fn<ReleaseFn>(Marshal.ReadIntPtr(factory), VtableRelease)(factory); }
        } catch { return false; }
    }

    private static T Fn<T>(nint vtable, int index) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(vtable, index * nint.Size));
}
