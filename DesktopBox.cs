using System;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class DesktopBox : Panel {
    readonly IntPtr host;
    readonly BoxData model;
    readonly Action save;
    readonly Label title=new Label();
    readonly ListView list=new ListView();
    readonly ImageList small=new ImageList(), large=new ImageList();
    readonly ContextMenuStrip menu=new ContextMenuStrip();
    Rectangle initial;
    Point mouse;
    int sides;
    bool gesture;
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    protected override CreateParams CreateParams {
        get {
            var cp=base.CreateParams; cp.Parent=host; cp.Style|=0x40000000;
            cp.ExStyle|=0x80000; return cp;
        }
    }

    internal DesktopBox(IntPtr desktop, BoxData b, Action changed, Action add, Action remove) {
        host=desktop; model=b; save=changed;
        Padding=new Padding(6);
        title.Height=30; title.Dock=DockStyle.Top; title.TextAlign=ContentAlignment.MiddleLeft;
        title.AutoEllipsis=true; title.Text=b.Title; title.Cursor=Cursors.SizeAll;
        small.ImageSize=SystemInformation.SmallIconSize; large.ImageSize=SystemInformation.IconSize;
        small.ColorDepth=large.ColorDepth=ColorDepth.Depth32Bit;
        list.SmallImageList=small; list.LargeImageList=large;
        list.BorderStyle=BorderStyle.None; list.Dock=DockStyle.Fill;
        list.FullRowSelect=true; list.HideSelection=false; list.ShowItemToolTips=true;
        list.HeaderStyle=ColumnHeaderStyle.None; list.Columns.Add("名称");
        list.Resize+=delegate { list.Columns[0].Width=Math.Max(40,list.ClientSize.Width-4); };
        Controls.Add(list); Controls.Add(title);
        StyleBox();
        foreach(var path in b.Files) Row(path);
        list.ItemActivate+=delegate {
            if(list.SelectedItems.Count==0) return;
            string path=(string)list.SelectedItems[0].Tag;
            if(!File.Exists(path) && !Directory.Exists(path)) { MessageBox.Show("路径不存在或无法访问。\n"+path); return; }
            try { Process.Start(new ProcessStartInfo(path){UseShellExecute=true}); }
            catch(Exception ex) { MessageBox.Show(ex.Message,"打开失败"); }
        };
        list.AllowDrop=true;
        list.DragEnter+=delegate(object sender, DragEventArgs e) {
            if(e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect=(e.AllowedEffect & DragDropEffects.Link)!=0 ? DragDropEffects.Link : e.AllowedEffect & DragDropEffects.Copy;
        };
        list.DragDrop+=delegate(object sender, DragEventArgs e) {
            var paths=e.Data.GetData(DataFormats.FileDrop) as string[];
            if(paths==null) return;
            var known=new HashSet<string>(b.Files,StringComparer.OrdinalIgnoreCase);
            list.BeginUpdate();
            try { foreach(var path in paths) if(known.Add(path)) { b.Files.Add(path); Row(path); } }
            finally { list.EndUpdate(); save(); }
        };
        menu.Items.Add("新建文件框",null,delegate { add(); });
        menu.Items.Add("重命名…",null,delegate { RenameBox(); });
        menu.Items.Add("颜色…",null,delegate {
            using(var d=new ColorDialog {Color=BackColor,FullOpen=true})
                if(d.ShowDialog()==DialogResult.OK) { model.Tint=d.Color.ToArgb(); StyleBox(); save(); }
        });
        var opacity=new ToolStripMenuItem("不透明度");
        foreach(int percent in new[]{55,70,85,92,100}) {
            int value=percent;
            opacity.DropDownItems.Add(value+"%",null,delegate { b.Alpha=255*value/100; StyleBox(); save(); });
        }
        menu.Items.Add(opacity);
        var blur=new ToolStripMenuItem("试用系统模糊") { Checked=b.Blur, CheckOnClick=true };
        blur.Click+=delegate {
            b.Blur=blur.Checked;
            if(!Native.SetBlur(Handle,b.Blur,BackColor,b.Alpha) && b.Blur) {
                blur.Checked=b.Blur=false; MessageBox.Show("此窗口未获系统模糊支持，将使用半透明背景。");
            }
            save();
        };
        menu.Items.Add(blur);
        menu.Items.Add("紧凑列表",null,delegate { b.Grid=false; StyleBox(); save(); });
        menu.Items.Add("桌面图标",null,delegate { b.Grid=true; StyleBox(); save(); });
        menu.Items.Add("移除选中引用",null,delegate {
            var selected=new List<ListViewItem>();
            foreach(ListViewItem row in list.SelectedItems) selected.Add(row);
            foreach(var row in selected) { b.Files.Remove((string)row.Tag); row.Remove(); }
            save();
        });
        menu.Items.Add("删除文件框…",null,delegate {
            if(MessageBox.Show("删除文件框及其引用？原文件将保留。","Desktop Boxes",MessageBoxButtons.YesNo)==DialogResult.Yes) remove();
        });
        ContextMenuStrip=list.ContextMenuStrip=title.ContextMenuStrip=menu;
        foreach(Control c in new Control[]{this,title}) {
            c.MouseDown+=Down; c.MouseMove+=MoveGesture; c.MouseUp+=Up;
            c.MouseCaptureChanged+=delegate { if(!c.Capture && gesture) EndGesture(); };
        }
    }
    internal void Mount() {
        Rectangle frame=model.Area, work=Screen.FromRectangle(frame).WorkingArea;
        frame.Width=Math.Min(work.Width,Math.Max(160,frame.Width));
        frame.Height=Math.Min(work.Height,Math.Max(112,frame.Height));
        frame.X=Math.Max(work.Left,Math.Min(work.Right-frame.Width,frame.X));
        frame.Y=Math.Max(work.Top,Math.Min(work.Bottom-frame.Height,frame.Y));
        var origin=frame.Location;
        if(!Native.ScreenToClient(host,ref origin)) throw new InvalidOperationException("无法转换桌面坐标");
        Bounds=new Rectangle(origin,frame.Size);
        CreateControl();
        if(Native.GetParent(Handle)!=host) throw new InvalidOperationException("Explorer 桌面父窗口校验失败");
        if(!SetWindowPos(Handle,IntPtr.Zero,origin.X,origin.Y,frame.Width,frame.Height,0x0050))
            throw new InvalidOperationException("文件框无法在桌面显示");
        StyleBox();
    }
    void StyleBox() {
        BackColor=title.BackColor=list.BackColor=Color.FromArgb(model.Tint);
        title.ForeColor=list.ForeColor=BackColor.GetBrightness()<0.58 ? Color.White : Color.Black;
        list.View=model.Grid ? View.LargeIcon : View.Details;
        if(IsHandleCreated) {
            Native.SetLayeredWindowAttributes(Handle,0,(byte)Math.Max(40,Math.Min(255,model.Alpha)),2);
            if(model.Blur) Native.SetBlur(Handle,true,BackColor,model.Alpha);
        }
    }
    void Row(string path) {
        bool folder=Directory.Exists(path);
        string key=folder ? "|directory" : "ext:"+Path.GetExtension(path).ToLowerInvariant();
        if(!small.Images.ContainsKey(key)) {
            small.Images.Add(key,Native.TypeIcon(path,folder,true));
            large.Images.Add(key,Native.TypeIcon(path,folder,false));
        }
        string name=Path.GetFileName(path.TrimEnd('\\'));
        list.Items.Add(new ListViewItem(name.Length==0 ? path : name,key){Tag=path,ToolTipText=path});
    }
    void RenameBox() {
        using(var d=new Form {Text="重命名",ClientSize=new Size(300,84),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterScreen})
        using(var input=new TextBox {Text=model.Title})
        using(var ok=new Button {Text="确定",DialogResult=DialogResult.OK}) {
            input.SetBounds(12,12,276,25); ok.SetBounds(204,46,84,27);
            d.Controls.AddRange(new Control[]{input,ok}); d.AcceptButton=ok; input.SelectAll();
            if(d.ShowDialog()==DialogResult.OK && input.Text.Trim().Length>0) { title.Text=model.Title=input.Text.Trim(); save(); }
        }
    }
    int Edges(Point p) {
        return (p.X<6 ? 1 : p.X>=Width-6 ? 2 : 0) | (p.Y<6 ? 4 : p.Y>=Height-6 ? 8 : 0);
    }
    void Down(object sender,MouseEventArgs e) {
        if(e.Button!=MouseButtons.Left) return;
        sides=sender==title ? 0 : Edges(e.Location); initial=Bounds; mouse=Cursor.Position;
        gesture=true; ((Control)sender).Capture=true;
    }
    static int Snap(int n) { return 8*(int)Math.Round(n/8.0); }
    void MoveGesture(object sender,MouseEventArgs e) {
        if(!gesture) {
            if(sender==this) {
                int edge=Edges(e.Location);
                Cursor=edge==5 || edge==10 ? Cursors.SizeNWSE : edge==6 || edge==9 ? Cursors.SizeNESW :
                    (edge&3)!=0 ? Cursors.SizeWE : Cursors.SizeNS;
            }
            return;
        }
        Point p=Cursor.Position;
        int dx=p.X-mouse.X,dy=p.Y-mouse.Y;
        int l=initial.Left,r=initial.Right,t=initial.Top,b=initial.Bottom;
        if(sides==0) { Location=new Point(Snap(l+dx),Snap(t+dy)); return; }
        if((sides&1)!=0) l=Math.Min(r-160,Snap(l+dx));
        if((sides&2)!=0) r=Math.Max(l+160,Snap(r+dx));
        if((sides&4)!=0) t=Math.Min(b-112,Snap(t+dy));
        if((sides&8)!=0) b=Math.Max(t+112,Snap(b+dy));
        Bounds=Rectangle.FromLTRB(l,t,r,b);
    }
    void Up(object sender,MouseEventArgs e) {
        if(!gesture) return;
        EndGesture(); ((Control)sender).Capture=false;
    }
    void EndGesture() { gesture=false; model.Area=new Rectangle(PointToScreen(Point.Empty),Size); save(); }
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if(disposing) { small.Dispose(); large.Dispose(); menu.Dispose(); }
    }
}