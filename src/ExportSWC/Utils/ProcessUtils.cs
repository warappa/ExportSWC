using System;
using System.Linq;

namespace ExportSWC.Utils
{
    public static class ProcessUtils
    {

        private static Func<string, bool, string> _processString;

        public static Func<string, bool, string> ProcessString => _processString;

        static ProcessUtils()
        {
            var flashDevelopAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => x.GetName().Name == "FlashDevelop")
                .FirstOrDefault();

            if (flashDevelopAssembly is null)
            {
                throw new Exception("Could not find FlashDevelop assembly!");
            }

            var argsProcessorType = flashDevelopAssembly.GetExportedTypes()
                .Where(x => x.Name == "ArgsProcessor")
                .First();

            var processStringMethod = argsProcessorType.GetMethods()
                .Where(x => x.Name == "ProcessString" && x.GetParameters().Length == 2)
                .First();

            _processString = (s, b) => (string)processStringMethod.Invoke(null,new object[] { s, b });
        }
    }
}
