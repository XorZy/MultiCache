namespace MultiCache.Config.Interactive
{
    using System;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json.Serialization;
    using LibConsole.Interactive;
    using LibConsole.Models;
    using LibConsole.Views;
    using MultiCache.Network;
    using Spectre.Console;
    public static class ObjectEditor
    {
        #region HANDLERS
        private static void HandleBool(object editedObject, PropertyInfo property)
        {
            property.SetValue(
                editedObject,
                BoolField.Prompt("Choose a value", (bool)property.GetValue(editedObject))
            );
        }
        private static void HandleDirectoryInfo(object editedObject, PropertyInfo property)
        {
            var directory = new ConsolePathField()
            {
                Buffer = property.GetValue(editedObject).ToString(),
                AllowEscape = true
            }.PromptDirectory();
            if (directory is not null)
            {
                property.SetValue(editedObject, directory);
            }
        }
        private static void HandleEnum(object editedObject, PropertyInfo property)
        {
            var value = ConsoleUtils.MultiChoice(
                "Choose a value",
                Enum.GetValues(property.PropertyType).Cast<object>()
            );
            property.SetValue(editedObject, value);
        }

        private static void HandleInt(object editedObject, PropertyInfo property)
        {
            while (true)
            {
                var value = new NumericField() { AllowEscape = true }.Prompt(
                    "Type a value",
                    (int)property.GetValue(editedObject)
                );
                if (value is not null)
                {
                    var range = property.GetCustomAttribute<RangeAttribute>();
                    if (range?.IsValid(value) == false)
                    {
                        AnsiConsole.WriteLine();
                        ConsoleUtils.Error("Value does not fall in the expected range");
                        continue;
                    }
                    property.SetValue(editedObject, value);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        private static void HandleString(object editedObject, PropertyInfo property)
        {
            var value = new ConsoleTextField() { AllowEscape = true }.Prompt(
                "Type a value",
                (string)property.GetValue(editedObject)
            );
            if (value is not null)
            {
                property.SetValue(editedObject, value);
            }
        }

        private static void HandleSpeed(object editedObject, PropertyInfo property)
        {
            var speed = InputReader.ReadSpeed();
            if (speed is not null)
            {
                property.SetValue(editedObject, speed);
            }
        }
        #endregion

        private static void DrawPropertyHeader(PropertyInfo info, object? reference)
        {
            AnsiConsole.Write(new Rule(info.Name) { Alignment = Justify.Left });
            AnsiConsole.WriteLine();
            var description = info.GetCustomAttribute<DescriptionAttribute>();
            if (description is not null)
            {
                AnsiConsole.Write(
                    new Panel($"[italic blue]{description.Description}[/]")
                    {
                        Border = BoxBorder.Rounded
                    }
                );
            }
            else
            {
                AnsiConsole.Write(
                    new Panel("[italic blue]No description available[/]")
                    {
                        Border = BoxBorder.Rounded
                    }
                );
            }
            AnsiConsole.WriteLine();

            if (reference is not null)
            {
                AnsiConsole.MarkupLine(
                    $"Default value: [bold]{reference.GetType().GetProperty(info.Name).GetValue(reference)}[/]"
                );
            }
            var range = info.GetCustomAttribute<RangeAttribute>();
            if (range is not null)
            {
                AnsiConsole.MarkupLine(
                    $"Acceptable range: [magenta bold][[{range.Minimum}-{range.Maximum}]][/]"
                );
            }
            AnsiConsole.Write(new Rule());
        }

        public static void EditSettings(
            object editedObject,
            FileInfo configFileInfo,
            object? reference
        )
        {
            Type[] handledTypes = new[]
            {
                typeof(int),
                typeof(bool),
                typeof(DirectoryInfo),
                typeof(string),
                typeof(Speed),
            };

            var settableSettings = editedObject
                .GetType()
                .GetProperties()
                .Where(
                    x =>
                    {
                        if (x.GetCustomAttribute<BrowsableAttribute>()?.Browsable == false)
                        {
                            return false;
                        }
                        return (
                                handledTypes.Contains(x.PropertyType)
                                || x.PropertyType.IsSubclassOf(typeof(Enum))
                            ) && !Attribute.IsDefined(x, typeof(JsonIgnoreAttribute));
                    }
                )
                .ToArray();

            while (true)
            {
                var setting = ConsoleUtils.MultiChoice(
                    "What setting do you want to change?",
                    settableSettings,
                    (x) => x.Name,
                    CustomChoice.Quit
                );

                if (setting == CustomChoice.Quit)
                {
                    StaticConfigurationParser.SerializeConfigFile(
                        editedObject,
                        configFileInfo.FullName,
                        reference
                    );
                    return;
                }

                if (setting is ValueChoice<PropertyInfo> vChoice)
                {
                    var property = vChoice.Choice;
                    DrawPropertyHeader(property, reference);
                    var propertyType = property.PropertyType;
                    while (true)
                    {
                        if (propertyType == typeof(bool))
                        {
                            HandleBool(editedObject, property);
                            break;
                        }
                        else if (propertyType == typeof(DirectoryInfo))
                        {
                            HandleDirectoryInfo(editedObject, property);
                            break;
                        }
                        else if (propertyType == typeof(int))
                        {
                            HandleInt(editedObject, property);
                            break;
                        }
                        else if (propertyType == typeof(string))
                        {
                            HandleString(editedObject, property);
                            break;
                        }
                        else if (propertyType == typeof(Speed))
                        {
                            HandleSpeed(editedObject, property);
                            break;
                        }
                        else if (propertyType.IsSubclassOf(typeof(Enum)))
                        {
                            HandleEnum(editedObject, property);
                            break;
                        }
                        else
                        {
                            throw new ArgumentException("Unhandled type!");
                        }
                    }
                }
            }
        }
    }
}
