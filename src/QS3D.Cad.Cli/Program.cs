using QS3D.Cad.Host;
using QS3D.Platform.InMemory;

var app = new StandaloneCadApplication();
app.NewDocument("Untitled");
var messageCursor = 0;

if (args.Length != 0)
{
    var result = app.ExecuteCommand(args[0], args.Skip(1));
    Console.WriteLine(result.Succeeded ? "OK" : "ERROR");
    DumpMessages(app, ref messageCursor);
    if (!result.Succeeded) Environment.ExitCode = 1;
    return;
}

Console.WriteLine("QS3D CAD bootstrap CLI. Type HELP, or EXIT to quit.");
while (true)
{
    Console.Write("Command: ");
    var line = Console.ReadLine();
    if (line is null || line.Equals("EXIT", StringComparison.OrdinalIgnoreCase)) break;
    if (line.Equals("HELP", StringComparison.OrdinalIgnoreCase))
    {
        var commands = app.Commands.Names
            .Concat(new[] { "UNDO", "REDO" })
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase);
        Console.WriteLine(string.Join(", ", commands));
        Console.WriteLine("HELP, EXIT");
        continue;
    }
    var result = app.Execute(line);
    if (!result.Succeeded) Console.WriteLine("ERROR " + result.Message);
    DumpMessages(app, ref messageCursor);
}

static void DumpMessages(StandaloneCadApplication app, ref int cursor)
{
    if (app.Documents.ActiveDocument?.Editor is not InMemoryEditor editor) return;
    if (cursor < 0 || cursor > editor.Messages.Count) cursor = 0;
    for (var index = cursor; index < editor.Messages.Count; index++)
        Console.WriteLine(editor.Messages[index]);
    cursor = editor.Messages.Count;
}
