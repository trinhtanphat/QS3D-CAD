using QS3D.Cad.Host;
using QS3D.Platform.InMemory;

var app = new StandaloneCadApplication();
app.NewDocument("Untitled");

if (args.Length != 0)
{
    var result = app.Execute(string.Join(' ', args));
    Console.WriteLine(result.Succeeded ? "OK" : "ERROR");
    DumpMessages(app);
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
        Console.WriteLine("LINE, CIRCLE, RECTANG, MOVE, SELECT, ERASE, LIST, UNDO, REDO");
        continue;
    }
    var result = app.Execute(line);
    if (!result.Succeeded) Console.WriteLine("ERROR " + result.Message);
    DumpMessages(app);
}

static void DumpMessages(StandaloneCadApplication app)
{
    if (app.Documents.ActiveDocument?.Editor is not InMemoryEditor editor) return;
    foreach (var message in editor.Messages) Console.WriteLine(message);
}
