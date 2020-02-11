using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
#if NETFRAMEWORK
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Security.Principal;
#endif
using Codartis.NsDepCop.Core.Interface;


namespace Codartis.NsDepCop.ServiceHost
{
    /// <summary>
    /// Host process for the dependency analyzer remoting service.
    /// </summary>
    /// <remarks>
    /// Monitors the parent process and quits if the parent no longer exists.
    /// </remarks>
    public class Program
    {
        /// <summary>
        /// Entry point of the application.
        /// </summary>
        /// <param name="args">The first parameter is the ID of the parent process.</param>
        /// <returns>Zero: normal exit. Negative value: error.</returns>
        public static int Main(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out var parentProcessId))
            {
                Usage();
                return -1;
            }

            try
            {
                RegisterRemotingService();

                WaitForParentProcessExit(parentProcessId);
                return 0;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"[{ProductConstants.ToolName}] ServiceHost exception caught: {e}");
                Console.Error.WriteLine($"Exception caught: {e}");
                return -2;
            }
        }


        private static void RegisterRemotingService()
        {
#if NETCOREAPP
            
            //var server = new DependencyAnalyzerService<RemoteDependencyAnalyzerServer>(new TextReaderWriterConnection(Console.In, Console.Out));
            //server.Start();
            
#elif NETFRAMEWORK
            ChannelServices.RegisterChannel(CreateIpcChannel(ServiceAddressProvider.PipeName), false);

            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(RemoteDependencyAnalyzerServer),
                ServiceAddressProvider.ServiceName, 
                WellKnownObjectMode.SingleCall);
#endif
        }

#if NETFRAMEWORK
        
        private static IpcChannel CreateIpcChannel(string portName)
        {
            var everyoneAccountName = GetAccountNameForSid(WellKnownSidType.WorldSid);

            var properties = new Hashtable
            {
                ["portName"] = portName,
                ["authorizedGroup"] = everyoneAccountName
            };

            return new IpcChannel(properties, null, null);
        }

        private static string GetAccountNameForSid(WellKnownSidType wellKnownSidType)
        {
            var sid = new SecurityIdentifier(wellKnownSidType, null);
            var account = (NTAccount) sid.Translate(typeof(NTAccount));
            return account.ToString();
        }

#endif

        private static void WaitForParentProcessExit(int parentProcessId)
        {
            var parentProcess = Process.GetProcesses().FirstOrDefault(i => i.Id == parentProcessId);
            parentProcess?.WaitForExit();
        }

        private static void Usage()
        {
            Console.Error.WriteLine($"Usage: {Assembly.GetExecutingAssembly().GetName().Name} <parentprocessid>");
        }
    }
}