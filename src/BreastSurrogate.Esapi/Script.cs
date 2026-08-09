using System;
using System.Runtime.CompilerServices;
using System.Windows;
using BreastSurrogate.Esapi.Esapi;
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

        string LogDirectory = @"\\Client\O$\ESAPI\WIPScripts\RH_scripting_wip\BreastSurrogate\Logs";
        
        /// <summary>
        /// Called by Eclipse when the script is launched.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            try
            {
                var esapiContext = new EsapiContext(context);
                var runner = new BreastSurrogateRunner(LogDirectory);
                runner.Run(esapiContext);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "BreastSurrogate could not start.\n\n" + exception.Message,
                    "BreastSurrogate",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
