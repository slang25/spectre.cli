using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Spectre.CommandLine.Internal.Configuration;

namespace Spectre.CommandLine.Internal.Modelling
{
    internal static class CommandModelBuilder
    {
        public static CommandModel Build(IConfiguration configuration)
        {
            var result = new List<CommandInfo>();
            foreach (var command in configuration.Commands)
            {
                result.Add(Build(null, command));
            }

            var model = new CommandModel(configuration.ApplicationName, result);
            CommandModelValidator.Validate(model);
            return model;
        }

        private static CommandInfo Build(CommandInfo parent, ConfiguredCommand command)
        {
            var info = new CommandInfo(parent, command);

            if (!info.IsProxy)
            {
                var description = info.CommandType.GetCustomAttribute<DescriptionAttribute>();
                if (description != null)
                {
                    info.Description = description.Description;
                }
            }

            foreach (var parameter in GetParameters(info))
            {
                info.Parameters.Add(parameter);
            }

            foreach (var childCommand in command.Children)
            {
                var child = Build(info, childCommand);
                info.Children.Add(child);
            }

            // Normalize argument positions.
            var index = 0;
            foreach (var argument in info.Parameters.OfType<CommandArgument>()
                .OrderBy(argument => argument.Position))
            {
                argument.Position = index;
                index++;
            }

            return info;
        }

        private static IEnumerable<CommandParameter> GetParameters(CommandInfo command)
        {
            var result = new List<CommandParameter>();
            var argumentPosition = 0;

            // We need to get parameters in order of the class where they were defined.
            // We assign each inheritance level a value that is used to properly sort the
            // arguments when iterating over them.
            IEnumerable<(int level, int sortOrder, PropertyInfo[] properties)> GetPropertiesInOrder()
            {
                var current = command.SettingsType;
                var level = 0;
                var sortOrder = 0;
                while (current.BaseType != null)
                {
                    yield return (level, sortOrder, current.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public));
                    current = current.BaseType;

                    // Things get a little bit complicated now.
                    // Only consider a setting's base type part of the
                    // setting, if there isn't a parent command that implements
                    // the setting's base type. This might come back to bite us :)
                    var currentCommand = command.Parent;
                    while (currentCommand != null)
                    {
                        if (currentCommand.SettingsType == current)
                        {
                            level--;
                            break;
                        }
                        currentCommand = currentCommand.Parent;
                    }

                    sortOrder--;
                }
            }

            var groups = GetPropertiesInOrder();
            foreach (var (_, _, properties) in groups.OrderBy(x => x.level).ThenBy(x => x.sortOrder))
            {
                var parameters = new List<CommandParameter>();

                foreach (var property in properties)
                {
                    if (property.IsDefined(typeof(CommandOptionAttribute)))
                    {
                        var attribute = property.GetCustomAttribute<CommandOptionAttribute>();
                        if (attribute != null)
                        {
                            var option = BuildOptionParameter(property, attribute);

                            // Any previous command has this option defined?
                            if (command.HaveParentWithOption(option))
                            {
                                // Do we allow it to exist on this command as well?
                                if (command.AllowParentOption(option))
                                {
                                    option.Required = false;
                                    option.IsShadowed = true;
                                    parameters.Add(option);
                                }
                            }
                            else
                            {
                                // No parent have this option.
                                parameters.Add(option);
                            }
                        }
                    }
                    else if (property.IsDefined(typeof(CommandArgumentAttribute)))
                    {
                        var attribute = property.GetCustomAttribute<CommandArgumentAttribute>();
                        if (attribute != null)
                        {
                            var argument = BuildArgumentParameter(property, attribute);

                            // Any previous command has this argument defined?
                            // In that case, we should not assign the parameter to this command.
                            if (!command.HaveParentWithArgument(argument))
                            {
                                parameters.Add(argument);
                            }
                        }
                    }
                }

                // Update the position for the parameters.
                foreach (var argument in parameters.OfType<CommandArgument>().OrderBy(x => x.Position))
                {
                    argument.Position = argumentPosition++;
                }

                // Add all parameters to the result.
                foreach (var groupResult in parameters)
                {
                    result.Add(groupResult);
                }
            }

            return result;
        }

        private static CommandOption BuildOptionParameter(PropertyInfo property, CommandOptionAttribute attribute)
        {
            var description = property.GetCustomAttribute<DescriptionAttribute>();
            var converter = property.GetCustomAttribute<TypeConverterAttribute>();
            var defaultValue = property.GetCustomAttribute<DefaultValueAttribute>();

            var kind = property.PropertyType == typeof(bool)
                ? ParameterKind.Flag
                : ParameterKind.Single;

            if (defaultValue == null && property.PropertyType == typeof(bool))
            {
                defaultValue = new DefaultValueAttribute(false);
            }

            return new CommandOption(property.PropertyType, kind,
                property, description?.Description, converter,
                attribute, defaultValue);
        }

        private static CommandArgument BuildArgumentParameter(PropertyInfo property, CommandArgumentAttribute attribute)
        {
            var description = property.GetCustomAttribute<DescriptionAttribute>();
            var converter = property.GetCustomAttribute<TypeConverterAttribute>();

            var kind = property.PropertyType == typeof(bool)
                ? ParameterKind.Flag
                : ParameterKind.Single;

            return new CommandArgument(property.PropertyType, kind,
                property, description?.Description, converter, attribute);
        }
    }
}
