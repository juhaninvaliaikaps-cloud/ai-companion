using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: CompilationRelaxations(8)]
[assembly: AssemblyVersion("0.0.0.0")]
namespace AdultPlayer;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		string text = null;
		string title = "Happi";
		bool loop = false;
		for (int i = 0; i < args.Length; i++)
		{
			switch (args[i])
			{
			case "-Path":
				if (i + 1 < args.Length)
				{
					text = args[++i];
				}
				break;
			case "-Title":
				if (i + 1 < args.Length)
				{
					title = args[++i];
				}
				break;
			case "-Loop":
				if (i + 1 < args.Length)
				{
					loop = args[++i] == "1";
				}
				break;
			}
		}
		if (!string.IsNullOrEmpty(text) && File.Exists(text))
		{
			Application.EnableVisualStyles();
			Application.Run(new AdultHostForm(text, loop, title, ProbeAspect(text)));
		}
	}

	private static double ProbeAspect(string path)
	{
		string text = FindTool("ffprobe.exe");
		if (text.Length == 0)
		{
			return 0.5625;
		}
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.FileName = text;
			processStartInfo.Arguments = "-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 \"" + path + "\"";
			processStartInfo.UseShellExecute = false;
			processStartInfo.CreateNoWindow = true;
			processStartInfo.RedirectStandardOutput = true;
			ProcessStartInfo startInfo = processStartInfo;
			using Process process = Process.Start(startInfo);
			string text2 = process.StandardOutput.ReadToEnd();
			process.WaitForExit(2000);
			string[] array = text2.Trim().Split(',');
			if (array.Length >= 2 && int.TryParse(array[0], out var result) && int.TryParse(array[1], out var result2) && result > 0 && result2 > 0)
			{
				return (double)result / (double)result2;
			}
		}
		catch
		{
		}
		return 0.5625;
	}

	private static string FindTool(string name)
	{
		string text = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		string[] array = text.Split(';');
		foreach (string text2 in array)
		{
			if (text2.Length != 0)
			{
				string text3 = Path.Combine(text2, name);
				if (File.Exists(text3))
				{
					return text3;
				}
			}
		}
		string text4 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\WinGet\\Links\\" + name);
		return File.Exists(text4) ? text4 : string.Empty;
	}
}
internal sealed class AdultHostForm : Form
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct WINDOWPOS
	{
		public IntPtr hwnd;

		public IntPtr hwndInsertAfter;

		public int x;

		public int y;

		public int cx;

		public int cy;

		public uint flags;
	}

	private const int WM_NCHITTEST = 132;

	private const int WM_NCLBUTTONDOWN = 161;

	private const int HTCLIENT = 1;

	private const int HTCAPTION = 2;

	private const int HTLEFT = 10;

	private const int HTRIGHT = 11;

	private const int HTTOP = 12;

	private const int HTTOPLEFT = 13;

	private const int HTTOPRIGHT = 14;

	private const int HTBOTTOM = 15;

	private const int HTBOTTOMLEFT = 16;

	private const int HTBOTTOMRIGHT = 17;

	private const int WS_CHILD = 1073741824;

	private const int WS_POPUP = int.MinValue;

	private const int GWL_STYLE = -16;

	private const int GWL_EXSTYLE = -20;

	private const int WS_EX_TRANSPARENT = 32;

	private const int WS_EX_NOACTIVATE = 134217728;

	private const uint SWP_NOZORDER = 4u;

	private const uint SWP_NOACTIVATE = 16u;

	private const uint SWP_FRAMECHANGED = 32u;

	private const int WM_CLOSE = 16;

	private const int WM_SIZING = 532;

	private const int WM_WINDOWPOSCHANGING = 70;

	private const uint SWP_NOSIZE = 1u;

	private const uint SWP_NOMOVE = 2u;

	private const int WMSZ_LEFT = 1;

	private const int WMSZ_RIGHT = 2;

	private const int WMSZ_TOP = 3;

	private const int WMSZ_TOPLEFT = 4;

	private const int WMSZ_TOPRIGHT = 5;

	private const int WMSZ_BOTTOM = 6;

	private const int WMSZ_BOTTOMLEFT = 7;

	private const int WMSZ_BOTTOMRIGHT = 8;

	private readonly string _path;

	private readonly bool _loop;

	private readonly string _title;

	private readonly double _aspect;

	private Process _player;

	private IntPtr _child;

	private Timer _watch;

	private Timer _resize;

	private Timer _dragPoll;

	private bool _dragging;

	private Point _dragCursor;

	private Point _dragWindow;

	private int _resizeEdge;

	public AdultHostForm(string path, bool loop, string title, double aspect)
	{
		_path = path;
		_loop = loop;
		_title = title;
		_aspect = ((aspect > 0.1 && aspect < 10.0) ? aspect : 0.5625);
		Text = title;
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.Manual;
		TopMost = true;
		ShowInTaskbar = true;
		BackColor = Color.FromArgb(10, 8, 13);
		int num = 420;
		int num2 = (int)Math.Round((double)(num - 12) / _aspect) + 46;
		MinimumSize = new Size(240, (int)Math.Round(228.0 / _aspect) + 46);
		ClientSize = new Size(num, num2);
		Left = Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Width - Width) / 2);
		Top = Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Height - Height) / 2);
		Panel panel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 34,
			BackColor = Color.FromArgb(18, 13, 23)
		};
		Label label = new Label
		{
			Text = title,
			ForeColor = Color.FromArgb(233, 221, 245),
			Font = new Font("Segoe UI", 9f),
			AutoSize = false,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(10, 0, 0, 0)
		};
		Button button = new Button
		{
			Text = "X",
			Dock = DockStyle.Right,
			Width = 38,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(184, 50, 70),
			ForeColor = Color.White,
			TabStop = false
		};
		button.FlatAppearance.BorderSize = 0;
		button.Click += (object s, EventArgs e) =>
		{
			Close();
		};
		panel.Controls.Add(label);
		panel.Controls.Add(button);
		EventHandler drag = (object s, EventArgs e) =>
		{
			ReleaseCapture();
			SendMessage(Handle, 161, (IntPtr)2, IntPtr.Zero);
		};
		MouseEventHandler value = (object s, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				drag(s, EventArgs.Empty);
			}
		};
		panel.MouseDown += value;
		label.MouseDown += value;
		label.Cursor = Cursors.SizeAll;
		panel.Cursor = Cursors.SizeAll;
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Top,
			Height = 7,
			BackColor = Color.FromArgb(18, 13, 23),
			Cursor = Cursors.SizeNS
		};
		panel2.MouseDown += (object s, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				Point point = PointToClient(Cursor.Position);
				int num3 = (_resizeEdge = ((point.X < 12) ? 13 : ((point.X > ClientSize.Width - 12) ? 14 : 12)));
				ReleaseCapture();
				SendMessage(Handle, 161, (IntPtr)num3, IntPtr.Zero);
			}
		};
		panel.Controls.Add(panel2);
		panel2.BringToFront();
		Controls.Add(panel);
		MouseDown += value;
		_dragPoll = new Timer
		{
			Interval = 15
		};
		_dragPoll.Tick += (object s, EventArgs e) =>
		{
			PollPictureDrag();
		};
		_dragPoll.Start();
		_resize = new Timer
		{
			Interval = 220
		};
		_resize.Tick += (object s, EventArgs e) =>
		{
			RestartPlayer();
		};
		Load += (object s, EventArgs e) =>
		{
			StartPlayer();
		};
		Resize += (object s, EventArgs e) =>
		{
			PlaceChild();
			ScheduleRestart();
		};
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			StopPlayer();
		};
	}

	private void StartPlayer()
	{
		string text = FindFfplay();
		if (text.Length == 0)
		{
			return;
		}
		_child = IntPtr.Zero;
		int num = Math.Max(120, ClientSize.Width - 12);
		int num2 = Math.Max(120, ClientSize.Height - 46);
		string arguments = "-noborder -autoexit " + (_loop ? "-loop 0 " : string.Empty) + "-x " + num + " -y " + num2 + " -window_title \"" + _title.Replace("\"", "'") + "\" \"" + _path + "\"";
		try
		{
			_player = Process.Start(new ProcessStartInfo
			{
				FileName = text,
				Arguments = arguments,
				UseShellExecute = false,
				CreateNoWindow = true
			});
		}
		catch
		{
			return;
		}
		_watch = new Timer
		{
			Interval = 100
		};
		_watch.Tick += (object s, EventArgs e) =>
		{
			if (_player == null || _player.HasExited)
			{
				_watch.Stop();
				Close();
			}
			else if (_player.MainWindowHandle != IntPtr.Zero)
			{
				_watch.Stop();
				AttachChild(_player.MainWindowHandle);
			}
		};
		_watch.Start();
	}

	private void ScheduleRestart()
	{
		if (_player != null && !(_child == IntPtr.Zero))
		{
			ShowWindow(_child, 0);
			_resize.Stop();
			_resize.Start();
		}
	}

	private void RestartPlayer()
	{
		_resize.Stop();
		if (_watch != null)
		{
			_watch.Stop();
		}
		if (_child != IntPtr.Zero)
		{
			ShowWindow(_child, 0);
			PostMessage(_child, 16, IntPtr.Zero, IntPtr.Zero);
			_child = IntPtr.Zero;
		}
		try
		{
			if (_player != null && !_player.HasExited)
			{
				_player.WaitForExit(800);
				if (!_player.HasExited)
				{
					_player.Kill();
				}
			}
		}
		catch
		{
		}
		_player = null;
		StartPlayer();
	}

	private void AttachChild(IntPtr child)
	{
		_child = child;
		SetParent(child, Handle);
		int windowLong = GetWindowLong(child, -16);
		windowLong = (windowLong | 0x40000000) & 0x7FFFFFFF;
		SetWindowLong(child, -16, windowLong);
		int windowLong2 = GetWindowLong(child, -20);
		SetWindowLong(child, -20, windowLong2 | 0x20 | 0x8000000);
		EnableWindow(child, enable: false);
		PlaceChild();
		ShowWindow(child, 5);
	}

	private void PlaceChild()
	{
		if (!(_child == IntPtr.Zero))
		{
			SetWindowPos(_child, IntPtr.Zero, 6, 38, Math.Max(120, ClientSize.Width - 12), Math.Max(120, ClientSize.Height - 46), 52u);
		}
	}

	private void PollPictureDrag()
	{
		if (!Visible)
		{
			return;
		}
		bool flag = (GetAsyncKeyState(1) & 0x8000) != 0;
		Point position = Cursor.Position;
		if (!_dragging)
		{
			if (flag)
			{
				Point point = PointToClient(position);
				if (point.X >= 12 && point.X <= ClientSize.Width - 12 && point.Y >= 38 && point.Y <= ClientSize.Height - 12)
				{
					_dragging = true;
					_dragCursor = position;
					_dragWindow = Location;
				}
			}
		}
		else if (!flag)
		{
			_dragging = false;
		}
		else
		{
			Location = new Point(_dragWindow.X + position.X - _dragCursor.X, _dragWindow.Y + position.Y - _dragCursor.Y);
		}
	}

	private void ResizeToAspect(ref Message m)
	{
		RECT structure = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
		int num = m.WParam.ToInt32();
		int num2 = Math.Max(240, structure.Right - structure.Left);
		int num3 = Math.Max(120, structure.Bottom - structure.Top);
		int val = num2;
		int val2 = num3;
		if (num == 3 || num == 6)
		{
			val = (int)Math.Round((double)(num3 - 46) * _aspect) + 12;
		}
		else
		{
			val2 = (int)Math.Round((double)(num2 - 12) / _aspect) + 46;
		}
		val = Math.Max(240, val);
		val2 = Math.Max((int)Math.Round(228.0 / _aspect) + 46, val2);
		if (num == 1 || num == 4 || num == 7)
		{
			structure.Left = structure.Right - val;
		}
		else
		{
			structure.Right = structure.Left + val;
		}
		if (num == 3 || num == 4 || num == 5)
		{
			structure.Top = structure.Bottom - val2;
		}
		else
		{
			structure.Bottom = structure.Top + val2;
		}
		Marshal.StructureToPtr(structure, m.LParam, fDeleteOld: false);
		m.Result = (IntPtr)1;
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 70)
		{
			WINDOWPOS structure = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
			if ((structure.flags & 1) == 0 && structure.cx > 0 && structure.cy > 0)
			{
				if (_resizeEdge == 3 || _resizeEdge == 6)
				{
					structure.cx = (int)Math.Round((double)(structure.cy - 46) * _aspect) + 12;
				}
				else
				{
					structure.cy = (int)Math.Round((double)(structure.cx - 12) / _aspect) + 46;
				}
				structure.cx = Math.Max(240, structure.cx);
				structure.cy = Math.Max((int)Math.Round(228.0 / _aspect) + 46, structure.cy);
				Marshal.StructureToPtr(structure, m.LParam, fDeleteOld: false);
			}
		}
		else if (m.Msg == 532)
		{
			ResizeToAspect(ref m);
		}
		else if (m.Msg == 132)
		{
			base.WndProc(ref m);
			if ((int)m.Result == 1)
			{
				Point point = PointToClient(Cursor.Position);
				bool flag = point.X < 7;
				bool flag2 = point.X >= ClientSize.Width - 7;
				bool flag3 = point.Y < 7;
				bool flag4 = point.Y >= ClientSize.Height - 7;
				if (flag && flag3)
				{
					m.Result = (IntPtr)13;
				}
				else if (flag2 && flag3)
				{
					m.Result = (IntPtr)14;
				}
				else if (flag && flag4)
				{
					m.Result = (IntPtr)16;
				}
				else if (flag2 && flag4)
				{
					m.Result = (IntPtr)17;
				}
				else if (flag)
				{
					m.Result = (IntPtr)10;
				}
				else if (flag2)
				{
					m.Result = (IntPtr)11;
				}
				else if (flag3)
				{
					m.Result = (IntPtr)12;
				}
				else if (flag4)
				{
					m.Result = (IntPtr)15;
				}
				else if (point.Y < 34)
				{
					m.Result = (IntPtr)2;
				}
				_resizeEdge = (int)m.Result;
			}
		}
		else
		{
			base.WndProc(ref m);
		}
	}

	private void StopPlayer()
	{
		try
		{
			if (_child != IntPtr.Zero)
			{
				ShowWindow(_child, 0);
				PostMessage(_child, 16, IntPtr.Zero, IntPtr.Zero);
				_child = IntPtr.Zero;
			}
			if (_player != null && !_player.HasExited)
			{
				_player.WaitForExit(800);
				if (!_player.HasExited)
				{
					_player.Kill();
				}
			}
		}
		catch
		{
		}
	}

	private static string FindFfplay()
	{
		string text = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		string[] array = text.Split(';');
		foreach (string text2 in array)
		{
			if (text2.Length != 0)
			{
				string text3 = Path.Combine(text2, "ffplay.exe");
				if (File.Exists(text3))
				{
					return text3;
				}
			}
		}
		string text4 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\WinGet\\Links\\ffplay.exe");
		return File.Exists(text4) ? text4 : string.Empty;
	}

	[DllImport("user32.dll")]
	private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr hwnd, int index);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hwnd, int command);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool EnableWindow(IntPtr hwnd, bool enable);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int key);
}
