using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace MinHooking.Test {
	internal static unsafe class Program {
		internal class ShowDialogAttribute : Attribute {
		}

		internal class ShowDialogOriginalStubAttribute : Attribute {
		}

		internal class RunAttribute : Attribute {
		}

		internal class RunOriginalStubAttribute : Attribute {
		}

		private static bool _isDaysLeftDialogShowed;

		private static void Main(string[] args) {
			MinHook showDialogHook;
			MinHook runHook;

			showDialogHook = MinHook.Create(typeof(Form).GetMethod("ShowDialog", new Type[] { typeof(IWin32Window) }), GetMethodByAttribute<ShowDialogAttribute>(), GetMethodByAttribute<ShowDialogOriginalStubAttribute>());
			showDialogHook.Enable();
			runHook = MinHook.Create(typeof(Application).GetMethod("Run", new Type[] { typeof(Form) }), GetMethodByAttribute<RunAttribute>(), GetMethodByAttribute<RunOriginalStubAttribute>());
			runHook.Enable();

			using (Form form = new Form { Text = "这个是不该显示的，如果显示了表示Hook失败" })
				form.ShowDialog();

			using (Form form = new Form { Text = "这个是应该显示的" })
				form.ShowDialog();

			using (Form form = new Form { Text = "这里应该显示NCK而不是Trial" }) {
				form.Controls.Add(new Label { Text = "Trial Edition", Size = new Size(100, 100) });
				Application.Run(form);
			}

			using (Form form = new Form { Text = "这里应该显示NCK而不是Trial" }) {
				form.Controls.Add(new Label { Text = "Trial Edition", Size = new Size(100, 100) });
				form.ShowDialog();
			}

			showDialogHook.Dispose();
			runHook.Dispose();

			using (Form form = new Form { Text = "这里应该显示Trial" }) {
				form.Controls.Add(new Label { Text = "Trial Edition", Size = new Size(100, 100) });
				Application.Run(form);
			}

			using (Form form = new Form { Text = "这里应该显示Trial" }) {
				form.Controls.Add(new Label { Text = "Trial Edition", Size = new Size(100, 100) });
				form.ShowDialog();
			}
		}

		private static MethodInfo GetMethodByAttribute<T>() where T : Attribute {
			foreach (MethodInfo methodInfo in typeof(Program).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)) {
				object[] attributes;

				attributes = methodInfo.GetCustomAttributes(typeof(T), false);
				if (attributes != null && attributes.Length != 0)
					return methodInfo;
			}
			return null;
		}

		[ShowDialog]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static DialogResult ShowDialog(Form self, IWin32Window owner) {
			if (!_isDaysLeftDialogShowed) {
				_isDaysLeftDialogShowed = true;
				return DialogResult.OK;
			}
			foreach (Control control in self.Controls)
				if (control is Label)
					control.Text = control.Text.Replace("Trial Edition", "[凉游浅笔深画眉, Wwh] / NCK");
			return ShowDialogOriginalStub(self, owner);
		}

		[Run]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void Run(Form mainForm) {
			foreach (Control control in mainForm.Controls)
				if (control is Label)
					control.Text = control.Text.Replace("Trial Edition", "[凉游浅笔深画眉, Wwh] / NCK");
			RunOriginalStub(mainForm);
		}

		[ShowDialogOriginalStub]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static DialogResult ShowDialogOriginalStub(Form self, IWin32Window owner) => throw new InvalidOperationException("Failed in hooking!");

		[RunOriginalStub]
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void RunOriginalStub(Form mainForm) => throw new InvalidOperationException("Failed in hooking!");
	}
}
