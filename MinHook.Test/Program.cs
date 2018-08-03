using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MinHooks.Test {
	internal static unsafe class Program {
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
			void* pFormDotShowDialogTarget;
			void* pFormDotShowDialogOriginal;
			void* pFormDotShowDialogOriginalStub;
			void* pApplicationDotRunTarget;
			void* pApplicationDotRunOriginal;
			void* pApplicationDotRunOriginalStub;
			void* temp;

			MinHookNative.MH_Initialize();
			pFormDotShowDialogTarget = GetMethodAddress(typeof(Form).GetMethod("ShowDialog", new Type[] { typeof(IWin32Window) }));
			void* pFormDotShowDialogDetour = GetMethodAddress(GetMethodByAttribute<FormDotShowDialogAttribute>());
			MinHookNative.MH_CreateHook(pFormDotShowDialogTarget, pFormDotShowDialogDetour, &pFormDotShowDialogOriginal);
			pFormDotShowDialogOriginalStub = GetMethodAddress(GetMethodByAttribute<FormDotShowDialogOriginalStubAttribute>());
			MinHookNative.MH_CreateHook(pFormDotShowDialogOriginalStub, pFormDotShowDialogOriginal, &temp);
			MinHookNative.MH_EnableHook(pFormDotShowDialogOriginalStub);
			MinHookNative.MH_EnableHook(pFormDotShowDialogTarget);
			pApplicationDotRunTarget = GetMethodAddress(typeof(Application).GetMethod("Run", new Type[] { typeof(Form) }));
			MinHookNative.MH_CreateHook(pApplicationDotRunTarget, GetMethodAddress(GetMethodByAttribute<ApplicationDotRunAttribute>()), &pApplicationDotRunOriginal);
			pApplicationDotRunOriginalStub = GetMethodAddress(GetMethodByAttribute<ApplicationDotRunOriginalStubAttribute>());
			MinHookNative.MH_CreateHook(pApplicationDotRunOriginalStub, pApplicationDotRunOriginal, &temp);
			MinHookNative.MH_EnableHook(pApplicationDotRunOriginalStub);
			MinHookNative.MH_EnableHook(pApplicationDotRunTarget);

			//void* hMinHook = LoadLibrary("MINHOOK64.dll");
			//Initialize = GetProcDelegate<MH_Initialize>(hMinHook, "MH_Initialize");
			//CreateHook = GetProcDelegate<MH_CreateHook>(hMinHook, "MH_CreateHook");
			//EnableHook = GetProcDelegate<MH_EnableHook>(hMinHook, "MH_EnableHook");
			//Initialize();
			//pFormDotShowDialogTarget = GetMethodAddress(typeof(Form).GetMethod("ShowDialog", new Type[] { typeof(IWin32Window) }));
			//void* pFormDotShowDialogDetour = GetMethodAddress(GetMethodByAttribute<FormDotShowDialogAttribute>());
			//CreateHook(pFormDotShowDialogTarget, pFormDotShowDialogDetour, &pFormDotShowDialogOriginal);
			//pFormDotShowDialogOriginalStub = GetMethodAddress(GetMethodByAttribute<FormDotShowDialogOriginalStubAttribute>());
			//CreateHook(pFormDotShowDialogOriginalStub, pFormDotShowDialogOriginal, &temp);
			//EnableHook(pFormDotShowDialogOriginalStub);
			//EnableHook(pFormDotShowDialogTarget);

			new Form {
				Text = "SplashScreen2 这个是不该显示的，如果显示了表示Hook失败"
			}.ShowDialog();

			new Form {
				Text = "SplashScreen2 这个是应该显示的"
			}.ShowDialog();

			Form form = new Form {
				Text = "SplashScreen2 这里应该显示NCK而不是Trial"
			};
			form.Controls.Add(new Label {
				Text = "Trial Edition"
			});
			Application.Run(form);

			form = new Form {
				Text = "SplashScreen2 这里应该显示NCK而不是Trial"
			};
			form.Controls.Add(new Label {
				Text = "Trial Edition"
			});
			form.ShowDialog();
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

		private static void* GetMethodAddress(MethodBase methodBase) {
			RuntimeHelpers.PrepareMethod(methodBase.MethodHandle);
			return (void*)methodBase.MethodHandle.GetFunctionPointer();
		}

		[FormDotShowDialog]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static DialogResult FormDotShowDialog(Form self, IWin32Window owner) {
			if (!_isDaysLeftDialogShowed) {
				_isDaysLeftDialogShowed = true;
				return DialogResult.OK;
			}
			foreach (Control control in self.Controls)
				if (control is Label)
					control.Text = control.Text.Replace("Trial Edition", "[凉游浅笔深画眉, Wwh] / NCK");
			return FormDotShowDialogOriginalStub(self, owner);
		}

		[FormDotShowDialogOriginalStub]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static DialogResult FormDotShowDialogOriginalStub(Form self, IWin32Window owner) => throw new InvalidOperationException("Failed in hooking!");

		[ApplicationDotRun]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void ApplicationDotRun(Form mainForm) {
			foreach (Control control in mainForm.Controls)
				if (control is Label)
					control.Text = control.Text.Replace("Trial Edition", "[凉游浅笔深画眉, Wwh] / NCK");
			ApplicationDotRunOriginalStub(mainForm);
		}

		[ApplicationDotRunOriginalStub]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void ApplicationDotRunOriginalStub(Form mainForm) => throw new InvalidOperationException("Failed in hooking!");
	}
}
