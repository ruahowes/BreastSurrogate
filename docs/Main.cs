using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Forms;
using ESAPI_rh;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]


namespace VMS.TPS
{
    class Program
    {
    
        [STAThread]
        static void Main(string[] args)
        {
            string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string strWorkPath = System.IO.Path.GetDirectoryName(strExeFilePath);
            string path = strWorkPath + @"\log_" + DateTime.Now.ToString(format: "yyyy_MM_dd_HH_mm_ss") + ".txt";
            try
            {
                using (VMS.TPS.Common.Model.API.Application app = VMS.TPS.Common.Model.API.Application.CreateApplication())
                {
                    Console.Write(value: "\nStarting test application.\n");
                    Execute(app, strWorkPath);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("\n\n" + e.ToString());
                LogWriter log = new LogWriter(e.ToString(), path, true);
                Console.Write(value: "\nPress any key to exit... \n");
                Console.ReadLine();
            }
        }
    
    
        static void Execute(VMS.TPS.Common.Model.API.Application app, string strWorkPath)
        {
            // TODO: Add your code here.
            string path = strWorkPath + @"\log_" + DateTime.Now.ToString(format: "yyyy_MM_dd_HH_mm_ss") + ".txt";
            LogWriter log = new LogWriter(path, true);
            //StandAloneMethods.GetImageInfo_Id_Study_Image(app, log);
            // StandAloneMethods.GetTreatedPatientPlans(app,log);
            // StandAloneMethods.GetImageInfo(app,log);
            // StandAloneMethods.GetTreatedPatientPlans_Photons(app,log,strWorkPath);
            //StandAloneMethods.GetScannerCouch(app,log);
            StandAloneMethods.GetPrescriptionsWithBolus(app, log);


            Console.Write(value: "\nComplete. Please press any key to exit.\n");
            Console.ReadLine();
            app.Dispose();
        }
    }
}
