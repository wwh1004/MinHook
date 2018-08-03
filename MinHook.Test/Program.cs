using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace MinHooks.Test {
	internal static unsafe class Program {
		internal class ShowDialogAttribute : Attribute {
		}

		internal class ShowDialogOriginalStubAttribute : Attribute {
		}

		internal class RunAttribute : Attribute {
		}

		internal class RunOriginalStubAttribute : Attribute {
		}

		//private delegate int MH_Initialize();

		//private delegate int MH_CreateHook(void* pTarget, void* pDetour, void** ppOriginal);

		//private delegate int MH_EnableHook(void* pTarget);

		//private static MH_Initialize Initialize;

		//private static MH_CreateHook CreateHook;

		//private static MH_EnableHook EnableHook;

		private static bool _isDaysLeftDialogShowed;

		//private static T GetProcDelegate<T>(void* hModule, string funcName) => (T)(object)Marshal.GetDelegateForFunctionPointer((IntPtr)GetProcAddress(hModule, funcName), typeof(T));

		//[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		//private static extern void* LoadLibrary(string lpLibFileName);

		//[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
		//private static extern void* GetProcAddress(void* hModule, string lpProcName);

		private static void Main(string[] args) {
			MinHook showDialogHook;
			MinHook runHook;

			showDialogHook = MinHook.Create(typeof(Form).GetMethod("ShowDialog", new Type[] { typeof(IWin32Window) }), GetMethodByAttribute<ShowDialogAttribute>(), GetMethodByAttribute<ShowDialogOriginalStubAttribute>());
			showDialogHook.Enable();
			runHook = MinHook.Create(typeof(Application).GetMethod("Run", new Type[] { typeof(Form) }), GetMethodByAttribute<RunAttribute>(), GetMethodByAttribute<RunOriginalStubAttribute>());
			runHook.Enable();

			new Form {
				Text = "这个是不该显示的，如果显示了表示Hook失败"
			}.ShowDialog();

			new Form {
				Text = "这个是应该显示的"
			}.ShowDialog();

			Form form = new Form {
				Text = "这里应该显示NCK而不是Trial"
			};
			form.Controls.Add(new Label {
				Text = "Trial Edition"
			});
			Application.Run(form);

			form = new Form {
				Text = "这里应该显示NCK而不是Trial"
			};
			form.Controls.Add(new Label {
				Text = "Trial Edition"
			});
			form.ShowDialog();
		}

		//void* hMinHook = LoadLibrary("MINHOOK64.dll");
		//Initialize = GetProcDelegate<MH_Initialize>(hMinHook, "MH_Initialize");
		//CreateHook = GetProcDelegate<MH_CreateHook>(hMinHook, "MH_CreateHook");
		//EnableHook = GetProcDelegate<MH_EnableHook>(hMinHook, "MH_EnableHook");
		//Initialize();
		//pShowDialogTarget = GetMethodAddress(typeof(Form).GetMethod("ShowDialog", new Type[] { typeof(IWin32Window) }));
		//void* pShowDialogDetour = GetMethodAddress(GetMethodByAttribute<ShowDialogAttribute>());
		//CreateHook(pShowDialogTarget, pShowDialogDetour, &pShowDialogOriginal);
		//pShowDialogOriginalStub = GetMethodAddress(GetMethodByAttribute<ShowDialogOriginalStubAttribute>());
		//CreateHook(pShowDialogOriginalStub, pShowDialogOriginal, &temp);
		//EnableHook(pShowDialogOriginalStub);
		//EnableHook(pShowDialogTarget);

		private static MethodInfo GetMethodByAttribute<T>() where T : Attribute {
			foreach (MethodInfo methodInfo in typeof(Program).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)) {
				object[] attributes;

				attributes = methodInfo.GetCustomAttributes(typeof(T), false);
				if (attributes != null && attributes.Length != 0)
					return methodInfo;
			}
			return null;
		}

		private static void* GetMethodAddress(MethodBase methodBase) {
			RuntimeHelpers.PrepareMethod(methodBase.MethodHandle);
			return (void*)methodBase.MethodHandle.GetFunctionPointer();
		}

		[ShowDialog]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static DialogResult ShowDialog(Form self, IWin32Window owner) {
			if (!_isDaysLeftDialogShowed) {
				_isDaysLeftDialogShowed = true;
				return DialogResult.OK;
			}
			foreach (Control control in self.Controls)
				if (control is Label)
					control.Text = control.Text.Replace("Trial Edition", "[凉游浅笔深画眉, Wwh] / NCK");
			return ShowDialogOriginalStub(self, owner);
		}

		[ShowDialogOriginalStub]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static DialogResult ShowDialogOriginalStub(Form self, IWin32Window owner) => throw new InvalidOperationException("Failed in hooking!");

		[Run]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void Run(Form mainForm) {
			foreach (Control control in mainForm.Controls)
				if (control is Label)
					control.Text = control.Text.Replace("Trial Edition", "[凉游浅笔深画眉, Wwh] / NCK");
			RunOriginalStub(mainForm);
		}

		[RunOriginalStub]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void RunOriginalStub(Form mainForm) => throw new InvalidOperationException("Failed in hooking!");
	}
}
