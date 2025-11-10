using ECommons.SimpleGui;

namespace Hyperborea.Gui;
public class LogWindow : Window
{
    public LogWindow() : base("Hyperborea 日志")
    {
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    public override void Draw()
    {
        InternalLog.PrintImgui();
    }
}
