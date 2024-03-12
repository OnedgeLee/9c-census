using Cocona;

namespace Census
{
    [HasSubCommands(typeof(Legacy), "legacy", Description = "Exports data from the legacy account")]
    public class Program
    {
        public static void Main(string[] args)
        {
            CoconaLiteApp.Run<Program>(args);
        }
    }
}
