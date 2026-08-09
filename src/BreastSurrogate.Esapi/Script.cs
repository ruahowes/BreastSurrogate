using System.Runtime.CompilerServices;
using Uclh.XRT.Esapi.Core;
using VMS.TPS.Common.Model.API;

namespace VMS.TPS
{
    /// <summary>
    /// Eclipse entry point for the read-only BreastSurrogate script.
    /// </summary>
    public class Script
    {
        public Script()
        {
        }

        /// <summary>
        /// Called by Eclipse when the script is launched.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            new EsapiContext(context);
        }
    }
}
