using System.Runtime.InteropServices;

namespace Typst.NET.Interop;

internal sealed class SafeCompilerHandle : SafeHandle
{
    public SafeCompilerHandle()
        : base(IntPtr.Zero, ownsHandle: true) { }

    public SafeCompilerHandle(nint handle)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (IsInvalid)
            return true;

        NativeMethods.typst_net_compiler_free(handle);

        return true;
    }
}
