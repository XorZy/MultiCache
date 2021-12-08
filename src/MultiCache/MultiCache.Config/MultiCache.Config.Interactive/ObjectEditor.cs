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
            object objectSettings,
            FileInfo configFileInfo,
            object? reference
        )
        {
            Type[] handledTypes = new[]
            {
                typeof(int),
                typeof(bool),
                typeof(string),
                typeof(Speed)
            };

            var settableSettings = objectSettings
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
                        objectSettings,
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
                            property.SetValue(
                                objectSettings,
                                BoolField.Prompt(
                                    "Choose a value",
                                    (bool)property.GetValue(objectSettings)
                                )
                            );
                            break;
                        }
                        else if (propertyType == typeof(int))
                        {
                            var value = new NumericField() { AllowEscape = true }.Prompt(
                                "Type a value",
                                (int)property.GetValue(objectSettings)
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
                                property.SetValue(objectSettings, value);
                                break;
                            }
                        }
                        else if (propertyType == typeof(string))
                        {
                            var value = new ConsoleTextField() { AllowEscape = true }.Prompt(
                                "Type a value",
                                (string)vChoice.Choice.GetValue(objectSettings)
                            );
                            if (value is not null)
                            {
                                property.SetValue(objectSettings, value);
                            }
                            break;
                        }
                        else if (propertyType == typeof(Speed))
                        {
                            var speed = InputReader.ReadSpeed();
                            if (speed is not null)
                            {
                                property.SetValue(objectSettings, speed);
                            }
                            break;
                        }
                        else if (propertyType.IsSubclassOf(typeof(Enum)))
                        {
                            var value = ConsoleUtils.MultiChoice(
                                "Choose a value",
                                Enum.GetValues(propertyType).Cast<object>()
                            );
                            property.SetValue(objectSettings, value);
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
