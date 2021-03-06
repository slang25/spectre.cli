using System;

namespace Spectre.Cli.Internal
{
    internal sealed class CommandAppSettings : ICommandAppSettings
    {
        public string? ApplicationName { get; set; }
        public IConsoleSettings? Console { get; set; }
        public ICommandInterceptor? Interceptor { get; set; }
        public ITypeRegistrarFrontend Registrar { get; set; }
        public bool PropagateExceptions { get; set; }
        public bool ValidateExamples { get; set; }
        public bool StrictParsing { get; set; }

        public ParsingMode ParsingMode =>
            StrictParsing ? ParsingMode.Strict : ParsingMode.Relaxed;

        public CommandAppSettings(ITypeRegistrar registrar)
        {
            Registrar = new TypeRegistrar(registrar);
        }

        public bool IsTrue(Func<CommandAppSettings, bool> func, string environmentVariableName)
        {
            if (func(this))
            {
                return true;
            }

            var environmentVariable = Environment.GetEnvironmentVariable(environmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentVariable))
            {
                if (environmentVariable.Equals("True", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}