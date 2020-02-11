using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Codartis.NsDepCop.Core.Interface.Analysis.Remote;
using Codartis.NsDepCop.Core.Util;

namespace Codartis.NsDepCop.Core.Implementation.Analysis.Remote
{
    /// <summary>
    /// Activates the out-of-process analyzer service.
    /// </summary>
    public static class AnalyzerServiceActivator
    {
        public static void /* IConnection */ Activate(MessageHandler traceMessageHandler)
        {
            var workingFolder = Assembly.GetExecutingAssembly().GetDirectory();
            var serviceExePath = Path.Combine(workingFolder, ServiceAddressProvider.ServiceHostProcessName + ".exe");

            CreateServer(workingFolder, serviceExePath, GetProcessId(), traceMessageHandler);
        }

        private static string GetProcessId() => Process.GetCurrentProcess().Id.ToString();

        private static void /* IConnection */ CreateServer(string workingFolderPath, string serviceExePath, string arguments, MessageHandler traceMessageHandler)
        {
            traceMessageHandler?.Invoke($"Starting {serviceExePath} with parameter {arguments}");
            traceMessageHandler?.Invoke($"  Working folder is {workingFolderPath}");

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "nsdepcop",
                    //FileName = serviceExePath,
                    WorkingDirectory = workingFolderPath,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                };
                var process = Process.Start(processStartInfo);
                //if (process != null) return new TextReaderWriterConnection(process.StandardOutput, process.StandardInput);
            }
            catch (Exception e)
            {
                traceMessageHandler?.Invoke($"AnalyzerServiceActivator.CreateServer failed: {e}");
            }

            // return null;
        }
    }
}
