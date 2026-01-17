using System.Runtime.InteropServices;

namespace Typst.NET.Interop;

internal sealed class SafeDocumentHandle : SafeHandle
{
    public SafeDocumentHandle()
        : base(IntPtr.Zero, ownsHandle: true) { }

    public SafeDocumentHandle(nint handle)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        // Document is NOT freed separately, it's owned by CompileResult
        // and freed by typst_net_result_free.
        // This SafeHandle just prevents use-after-free
        return true;
    }
}
