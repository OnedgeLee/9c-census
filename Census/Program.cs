﻿using Cocona;

namespace Census
{
    [HasSubCommands(typeof(Account), "account", Description = "Exports data from a generic account")]
    [HasSubCommands(typeof(Legacy), "legacy", Description = "Exports data from the legacy account")]
    public class Program
    {
        public static void Main(string[] args)
        {
            CoconaLiteApp.Run<Program>(args);
        }
    }
}
