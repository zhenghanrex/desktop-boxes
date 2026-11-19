using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Threading;
using System.Reflection;

[assembly: AssemblyTitle("Desktop Boxes")]
[assembly: AssemblyVersion("1.0.0.0")]
public class BoxData {
    public string Title = "文件框";
    public Rectangle Area = new Rectangle(80,80,320,260);
    public int Tint = Color.FromArgb(46,62,78).ToArgb();
    public int Alpha = 224;
    public bool Grid, Blur;
    public List<string> Files = new List<string>();
}
internal static class Layout {
    static readonly XmlSerializer Serializer = new XmlSerializer(typeof(List<BoxData>));
    public static List<BoxData> Read(string path) {
        using(var stream=File.OpenRead(path)) return (List<BoxData>)Serializer.Deserialize(stream);
    }
    public static void Write(string path, List<BoxData> boxes) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+".tmp";
        using(var stream=File.Create(temp)) { Serializer.Serialize(stream,boxes); stream.Flush(true); }
        if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
    }
}
internal sealed class ShellEvents : NativeWindow, IDisposable {
    readonly int shellMessage=Native.RegisterWindowMessage("TaskbarCreated");
    readonly Action refresh;
    public ShellEvents(Action action) { refresh=action; CreateHandle(new CreateParams()); }
    protected override void WndProc(ref Message m) {
        base.WndProc(ref m);
        if(m.Msg==shellMessage || m.Msg==0x007e) refresh();
    }
    public void Dispose() { DestroyHandle(); }
}
internal sealed class BoxesApp : ApplicationContext {
    readonly string file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopBoxes","layout.xml");
    readonly NotifyIcon tray=new NotifyIcon();
    readonly ContextMenuStrip menu=new ContextMenuStrip();
    readonly List<DesktopBox> windows=new List<DesktopBox>();
    readonly List<BoxData> boxes;
    readonly ShellEvents events;
    readonly System.Windows.Forms.Timer retry=new System.Windows.Forms.Timer();
    int retries;
    IntPtr host;
    internal BoxesApp() {
        boxes=File.Exists(file) ? Layout.Read(file) : new List<BoxData> { new BoxData() };
        menu.Items.Add("新建文件框",null,delegate { Add(); });
        menu.Items.Add("重新连接桌面",null,delegate { Connect(); });
        menu.Items.Add("退出",null,delegate { ExitThread(); });
        tray.Text="Desktop Boxes"; tray.Icon=SystemIcons.Application; tray.ContextMenuStrip=menu; tray.Visible=true;
        retry.Interval=1000; retry.Tick+=delegate { Connect(); };
        events=new ShellEvents(delegate { retries=0; retry.Start(); });
        Connect();
    }
    void Save() {
        try { Layout.Write(file,boxes); }
        catch(Exception ex) { MessageBox.Show(ex.Message,"保存布局失败"); }
    }
    void Connect() {
        foreach(var w in windows) w.Dispose(); windows.Clear();
        host=Native.Desktop();
        if(host==IntPtr.Zero) {
            if(++retries<15) retry.Start();
            else { retry.Stop(); retries=0; tray.ShowBalloonTip(4000,"Desktop Boxes","未找到桌面。可从托盘重新连接。",ToolTipIcon.Warning); }
            return;
        }
        retry.Stop(); retries=0;
        foreach(var model in boxes) Mount(model);
    }
    void Mount(BoxData model) {
        DesktopBox box=null;
        box=new DesktopBox(host,model,Save,Add,delegate {
            boxes.Remove(model); windows.Remove(box); box.Dispose(); Save();
        });
        try { box.Mount(); windows.Add(box); }
        catch(Exception ex) { box.Dispose(); tray.ShowBalloonTip(5000,"桌面挂载失败",ex.Message,ToolTipIcon.Error); }
    }
    void Add() {
        if(host==IntPtr.Zero || host!=Native.Desktop()) Connect();
        if(host==IntPtr.Zero) return;
        var work=Screen.FromPoint(Cursor.Position).WorkingArea;
        int offset=32+24*(boxes.Count%8);
        var b=new BoxData { Area=new Rectangle(work.Left+offset,work.Top+offset,320,260) };
        boxes.Add(b); Mount(b); Save();
    }
    protected override void ExitThreadCore() {
        retry.Dispose(); events.Dispose();
        foreach(var w in windows) w.Dispose();
        tray.Visible=false; tray.Dispose(); menu.Dispose(); base.ExitThreadCore();
    }
}
internal static class Program {
    [STAThread] static int Main(string[] args) {
        if(args.Length==2 && args[0]=="--self-test") {
            var b=new BoxData { Title="收件箱", Grid=true }; b.Files.Add(@"C:\example.lnk");
            var data=new List<BoxData>{b}; Layout.Write(args[1],data);
            b.Alpha=171; Layout.Write(args[1],data); var copy=Layout.Read(args[1])[0];
            return copy.Files.Count==1 && copy.Title==b.Title && copy.Area==b.Area && copy.Grid && copy.Alpha==171 && Layout.Read(args[1]+".bak")[0].Alpha==224 ? 0 : 1;
        }
        bool first;
        using(var mutex=new Mutex(true,@"Local\DesktopBoxes-1",out first)) {
            if(!first) return 0;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new BoxesApp()); }
            catch(Exception ex) { MessageBox.Show(ex.Message,"Desktop Boxes"); return 1; }
        }
        return 0;
    }
}