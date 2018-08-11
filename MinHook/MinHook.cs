using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using static MinHooks.MinHookNative;

namespace MinHooks {
	public sealed unsafe class MinHook : IDisposable {
		private readonly void* _pTarget;

		private readonly void* _pOriginalStub;

		private bool _isDisposed;

		static MinHook() => MH_Initialize();

		private MinHook(void* pTarget, void* pDetour, void* pOriginalStub) {
			if (pTarget == null)
				throw new ArgumentNullException(nameof(pTarget));
			if (pDetour == null)
				throw new ArgumentNullException(nameof(pDetour));

			MH_STATUS status;
			void* pOriginal;

			_pTarget = pTarget;
			status = MH_CreateHook(pTarget, pDetour, &pOriginal);
			if (status != MH_STATUS.MH_OK)
				throw new InvalidOperationException(MH_StatusToString(status));
			if (pOriginalStub == null)
				return;
			_pOriginalStub = pOriginalStub;
			status = MH_CreateHook(pOriginalStub, pOriginal, null);
			if (status != MH_STATUS.MH_OK)
				throw new InvalidOperationException(MH_StatusToString(status));
		}

		public static MinHook Create(MethodBase target, MethodBase detour) => Create(GetMethodAddress(target), GetMethodAddress(detour), IntPtr.Zero);

		public static MinHook Create(MethodBase target, MethodBase detour, MethodBase originalStub) => Create(GetMethodAddress(target), GetMethodAddress(detour), GetMethodAddress(originalStub));

		public static MinHook Create(IntPtr pTarget, IntPtr pDetour, IntPtr pOriginalStub) => Create((void*)pTarget, (void*)pDetour, (void*)pOriginalStub);

		public static MinHook Create(void* pTarget, void* pDetour, void* pOriginalStub) {
			try {
				return new MinHook(pTarget, pDetour, pOriginalStub);
			}
			catch (InvalidOperationException) {
				return null;
			}
		}

		public static IntPtr GetMethodAddress(MethodBase method) {
			if (method == null)
				throw new ArgumentNullException(nameof(method));

			RuntimeHelpers.PrepareMethod(method.MethodHandle);
			return method.MethodHandle.GetFunctionPointer();
		}

		public bool Enable() => (_pOriginalStub == null ? true : MH_EnableHook(_pOriginalStub) == MH_STATUS.MH_OK) && MH_EnableHook(_pTarget) == MH_STATUS.MH_OK;

		public bool Disable() => MH_DisableHook(_pTarget) == MH_STATUS.MH_OK && (_pOriginalStub == null ? true : MH_DisableHook(_pOriginalStub) == MH_STATUS.MH_OK);

		public void Dispose() {
			if (_isDisposed)
				return;

			Disable();
			MH_RemoveHook(_pTarget);
			if (_pOriginalStub != null)
				MH_RemoveHook(_pOriginalStub);
			_isDisposed = true;
		}
	}
}
