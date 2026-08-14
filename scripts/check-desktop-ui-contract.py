from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
XAML = ROOT / "src" / "QS3D.Cad.Desktop" / "MainWindow.xaml"

text = XAML.read_text(encoding="utf-8")

required = {
    "CAD ribbon control": 'Style="{StaticResource RibbonTabControl}"',
    "Home ribbon tab": 'Header="Home"',
    "Insert ribbon tab": 'Header="Insert"',
    "Annotate ribbon tab": 'Header="Annotate"',
    "View ribbon tab": 'Header="View"',
    "Manage ribbon tab": 'Header="Manage"',
    "project navigator": 'Text="PROJECT NAVIGATOR"',
    "properties palette": 'Text="PROPERTIES"',
    "model viewport": 'x:Name="ViewportCanvas"',
    "reference qualification label": 'Text="REFERENCE 2D VIEWPORT"',
    "command line": 'x:Name="CommandBox"',
    "model tab": 'Text="Model"',
    "grid status": 'Text="GRID"',
    "selection tool identity": 'x:Name="SelectToolButton"',
    "line tool identity": 'x:Name="LineToolButton"',
    "rectangle tool identity": 'x:Name="RectangleToolButton"',
    "circle tool identity": 'x:Name="CircleToolButton"',
    "document navigator": 'x:Name="DocumentList"',
    "entity navigator": 'x:Name="EntityList"',
    "layer navigator": 'x:Name="LayerList"',
    "selection properties": 'x:Name="PropertyList"',
    "host messages": 'x:Name="MessageList"',
    "reference-only native controls": 'IsEnabled="False"',
}

missing = [label for label, token in required.items() if token not in text]
if missing:
    raise SystemExit("Desktop UI contract missing: " + ", ".join(missing))

for forbidden in ("Autodesk", "AutoCAD", "Bricsys", "BricsCAD"):
    if forbidden in text:
        raise SystemExit(f"Desktop UI must not copy or brand itself with third-party CAD identity: {forbidden}")

if text.count('Style="{StaticResource RibbonButton}"') < 12:
    raise SystemExit("Desktop UI ribbon regressed below the grouped professional tool baseline.")

if text.count("GridSplitter") < 2:
    raise SystemExit("Desktop UI must keep independently resizable navigator and properties palettes.")

print("QS3D-CAD desktop UI contract PASS")
