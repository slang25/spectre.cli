﻿using System.ComponentModel;

namespace Spectre.CommandLine.Tests.Data
{
    public class AnimalSettings
    {
        [CommandOption("-a|--alive")]
        [Description("Indicates whether or not the animal is alive.")]
        public bool IsAlive { get; set; }

        [CommandArgument(1, "[LEGS]")]
        [Description("The number of legs.")]
        public int Legs { get; set; }
    }
}