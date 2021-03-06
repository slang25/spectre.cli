using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Cli.Exceptions;

namespace Spectre.Cli.Internal
{
    internal static class CommandModelValidator
    {
        public static void Validate(CommandModel model, CommandAppSettings settings)
        {
            if (model.Commands.Count == 0 && model.DefaultCommand == null)
            {
                throw ConfigurationException.NoCommandConfigured();
            }

            foreach (var command in model.Commands)
            {
                // Alias collision?
                foreach (var alias in command.Aliases)
                {
                    if (model.Commands.Any(x => x.Name.Equals(alias, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw ConfigurationException.CommandNameConflict(command, alias);
                    }
                }
            }

            Validate(model.DefaultCommand);
            foreach (var command in model.Commands)
            {
                Validate(command);
            }

            if (settings.ValidateExamples)
            {
                ValidateExamples(model);
            }
        }

        private static void Validate(CommandInfo? command)
        {
            if (command == null)
            {
                return;
            }

            // Get duplicate options for command.
            var duplicateOptions = GetDuplicates(command);
            if (duplicateOptions.Length > 0)
            {
                throw ConfigurationException.DuplicateOption(command, duplicateOptions);
            }

            // No children?
            if (command.IsBranch && command.Children.Count == 0)
            {
                throw ConfigurationException.BranchHasNoChildren(command);
            }

            // Multiple vector arguments?
            var arguments = command.Parameters.OfType<CommandArgument>();
            if (arguments.Any(x => x.ParameterKind == ParameterKind.Vector))
            {
                // Multiple vector arguments for command?
                if (arguments.Count(x => x.ParameterKind == ParameterKind.Vector) > 1)
                {
                    throw ConfigurationException.TooManyVectorArguments(command);
                }

                // Make sure that vector arguments are specified last.
                if (arguments.Last().ParameterKind != ParameterKind.Vector)
                {
                    throw ConfigurationException.VectorArgumentNotSpecifiedLast(command);
                }
            }

            // Arguments
            var argumnets = command.Parameters.OfType<CommandArgument>();
            foreach (var argument in arguments)
            {
                if (argument.Required && argument.DefaultValue != null)
                {
                    throw ConfigurationException.RequiredArgumentsCannotHaveDefaultValue(argument);
                }
            }

            // Options
            var options = command.Parameters.OfType<CommandOption>();
            foreach (var option in options)
            {
                // Pair deconstructable?
                if (option.Property.PropertyType.IsPairDeconstructable())
                {
                    if (option.PairDeconstructor != null && option.Converter != null)
                    {
                        throw ConfigurationException.OptionBothHasPairDeconstructorAndTypeParameter(option);
                    }
                }
                else if (option.PairDeconstructor != null)
                {
                    throw ConfigurationException.OptionTypeDoesNotSupportDeconstruction(option);
                }

                // Optional options that are not flags?
                if (option.ParameterKind == ParameterKind.FlagWithValue && !option.IsFlagValue())
                {
                    throw ConfigurationException.OptionalOptionValueMustBeFlagWithValue(option);
                }
            }

            // Validate child commands.
            foreach (var childCommand in command.Children)
            {
                Validate(childCommand);
            }
        }

        private static void ValidateExamples(CommandModel model)
        {
            var examples = new List<string[]>();
            examples.AddRangeIfNotNull(model.Examples);

            // Get all examples.
            var queue = new Queue<ICommandContainer>(new[] { model });
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var command in current.Commands)
                {
                    examples.AddRangeIfNotNull(command.Examples);
                    queue.Enqueue(command);
                }
            }

            // Validate all examples.
            foreach (var example in examples)
            {
                try
                {
                    var parser = new CommandTreeParser(model, ParsingMode.Strict);
                    parser.Parse(example);
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException("Validation of examples failed.", ex);
                }
            }
        }

        private static string[] GetDuplicates(CommandInfo command)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);

            void AddToResult(IEnumerable<string> keys)
            {
                foreach (var key in keys)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        if (!result.ContainsKey(key))
                        {
                            result.Add(key, 0);
                        }

                        result[key]++;
                    }
                }
            }

            foreach (var option in command.Parameters.OfType<CommandOption>())
            {
                AddToResult(option.ShortNames);
                AddToResult(option.LongNames);
            }

            return result.Where(x => x.Value > 1)
                .Select(x => x.Key).ToArray();
        }
    }
}
