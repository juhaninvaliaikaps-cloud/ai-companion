param(
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$Loop = 1,
    [string]$Title = "Happi",
    [int]$Left = -1,
    [int]$Top = -1
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class AdultHostForm : Form
{
    const int WM_NCHITTEST = 0x84;
    const int WM_NCLBUTTONDOWN = 0xA1;
    const int HTCLIENT = 1;
    const int HTCAPTION = 2;
    const int HTLEFT = 10;
    const int HTRIGHT = 11;
    const int HTTOP = 12;
    const int HTTOPLEFT = 13;
    const int HTTOPRIGHT = 14;
    const int HTBOTTOM = 15;
    const int HTBOTTOMLEFT = 16;
    const int HTBOTTOMRIGHT = 17;
    const int WS_CHILD = 0x40000000;
    const int WS_POPUP = unchecked((int)0x80000000);
    const int GWL_STYLE = -16;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_FRAMECHANGED = 0x0020;

    readonly string _path;
    readonly bool _loop;
    readonly string _title;
    Process _player;
    IntPtr _child;
    Timer _timer;

    public AdultHostForm(string path, bool loop, string title, int left, int top)
    {
        _path = path;
        _loop = loop;
        _title = title;
        Text = title;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(10, 8, 13);
        MinimumSize = new Size(240, 260);
        ClientSize = new Size(544, 640);
        if (left >= 0) Left = left;
        else Left = Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Width - Width) / 2);
        if (top >= 0) Top = top;
        else Top = Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Height - Height) / 2);

        var header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(18, 13, 23) };
        var label = new Label
        {
            Text = title,
            ForeColor = Color.FromArgb(233, 221, 245),
            Font = new Font("Segoe UI", 9f),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
        };
        var close = new Button
        {
            Text = "X",
            Dock = DockStyle.Right,
            Width = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(184, 50, 70),
            ForeColor = Color.White,
            TabStop = false,
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (s, e) => Close();
        header.Controls.Add(label);
        header.Controls.Add(close);
        header.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
        };
        Controls.Add(header);

        Load += (s, e) => StartPlayer();
        Resize += (s, e) => PlaceChild();
        FormClosed += (s, e) => StopPlayer();
    }

    void StartPlayer()
    {
        var ffplay = FindFfplay();
        if (ffplay.Length == 0)
            return;
        var width = Math.Max(120, ClientSize.Width - 12);
        var height = Math.Max(120, ClientSize.Height - 46);
        var args = "-noborder -autoexit "
            + (_loop ? "-loop 0 " : string.Empty)
            + "-alwaysontop -x " + width + " -y " + height + " "
            + "-window_title \"" + _title.Replace("\"", "'") + "\" "
            + "\"" + _path + "\"";
        try
        {
            _player = Process.Start(new ProcessStartInfo
            {
                FileName = ffplay,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch
        {
            return;
        }
        _timer = new Timer { Interval = 100 };
        _timer.Tick += (s, e) =>
        {
            if (_player == null || _player.HasExited)
            {
                _timer.Stop();
                Close();
                return;
            }
            if (_player.MainWindowHandle != IntPtr.Zero)
            {
                _timer.Stop();
                AttachChild(_player.MainWindowHandle);
            }
        };
        _timer.Start();
    }

    void AttachChild(IntPtr child)
    {
        _child = child;
        SetParent(child, Handle);
        var style = GetWindowLong(child, GWL_STYLE);
        style = (style | WS_CHILD) & ~WS_POPUP;
        SetWindowLong(child, GWL_STYLE, style);
        PlaceChild();
        ShowWindow(child, 5);
    }

    void PlaceChild()
    {
        if (_child == IntPtr.Zero)
            return;
        SetWindowPos(_child, IntPtr.Zero, 6, 38,
            Math.Max(120, ClientSize.Width - 12),
            Math.Max(120, ClientSize.Height - 46),
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);
            if ((int)m.Result == HTCLIENT)
            {
                var p = PointToClient(Cursor.Position);
                const int b = 7;
                var left = p.X < b;
                var right = p.X >= ClientSize.Width - b;
                var top = p.Y < b;
                var bottom = p.Y >= ClientSize.Height - b;
                if (left && top) m.Result = (IntPtr)HTTOPLEFT;
                else if (right && top) m.Result = (IntPtr)HTTOPRIGHT;
                else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (left) m.Result = (IntPtr)HTLEFT;
                else if (right) m.Result = (IntPtr)HTRIGHT;
                else if (top) m.Result = (IntPtr)HTTOP;
                else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                else if (p.Y < 34) m.Result = (IntPtr)HTCAPTION;
            }
            return;
        }
        base.WndProc(ref m);
    }

    void StopPlayer()
    {
        try
        {
            if (_player != null && !_player.HasExited)
                _player.Kill();
        }
        catch
        {
        }
    }

    static string FindFfplay()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var folder in path.Split(';'))
        {
            if (folder.Length == 0)
                continue;
            var candidate = Path.Combine(folder, "ffplay.exe");
            if (File.Exists(candidate))
                return candidate;
        }
        var winget = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft\\WinGet\\Links\\ffplay.exe");
        return File.Exists(winget) ? winget : string.Empty;
    }

    [DllImport("user32.dll")]
    static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")]
    static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies System.Windows.Forms, System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()
$form = New-Object AdultHostForm($Path, ($Loop -eq 1), $Title, $Left, $Top)
[System.Windows.Forms.Application]::Run($form)
