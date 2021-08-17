using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;

namespace NeoDebug.VS
{
    static class Extensions
    {
        public static (string slnDir, string slnFile, string userOptsFile) GetSolutionInfo(this IVsSolution @this)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var hresult = @this.GetSolutionInfo(out var slnDir, out var slnFile, out var userOptsFile);
            if (hresult != VSConstants.S_OK)
            {
                throw new COMException("GetSolutionInfo failed", hresult);
            }

            return (slnDir, slnFile, userOptsFile);
        }
    }
}
