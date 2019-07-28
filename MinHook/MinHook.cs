using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using static MinHooking.MinHookNative;

namespace MinHooking {
	public sealed unsafe class MinHook : IDisposable {
		private void* _pTarget;
		private void* _pOriginalStub;
		private bool _isDisposed;

		static MinHook() {
			MH_Initialize();
		}

		private MinHook() {
		}

		public static MinHook Create(MethodBase target, MethodBase detour) {
			return Create(GetMethodAddress(target), GetMethodAddress(detour), IntPtr.Zero);
		}

		public static MinHook Create(MethodBase target, MethodBase detour, MethodBase originalStub) {
			return Create(GetMethodAddress(target), GetMethodAddress(detour), GetMethodAddress(originalStub));
		}

		public static MinHook Create(IntPtr pTarget, IntPtr pDetour, IntPtr pOriginalStub) {
			return Create((void*)pTarget, (void*)pDetour, (void*)pOriginalStub);
		}

		public static MinHook Create(void* pTarget, void* pDetour, void* pOriginalStub) {
			if (pTarget is null)
				throw new ArgumentNullException(nameof(pTarget));
			if (pDetour is null)
				throw new ArgumentNullException(nameof(pDetour));
			if (pOriginalStub is null)
				throw new ArgumentNullException(nameof(pOriginalStub));

			MH_STATUS status;
			void* pOriginal;

			status = MH_CreateHook(pTarget, pDetour, &pOriginal);
			if (status != MH_STATUS.MH_OK)
				return null;
			status = MH_CreateHook(pOriginalStub, pOriginal, null);
			if (status != MH_STATUS.MH_OK) {
				MH_RemoveHook(pTarget);
				return null;
			}
			return new MinHook {
				_pTarget = pTarget,
				_pOriginalStub = pOriginalStub
			};
		}

		public static IntPtr GetMethodAddress(MethodBase method) {
			if (method is null)
				throw new ArgumentNullException(nameof(method));

			RuntimeHelpers.PrepareMethod(method.MethodHandle);
			return method.MethodHandle.GetFunctionPointer();
		}

		public bool Enable() {
			return (_pOriginalStub is null ? true : MH_EnableHook(_pOriginalStub) == MH_STATUS.MH_OK) && MH_EnableHook(_pTarget) == MH_STATUS.MH_OK;
		}

		public bool Disable() {
			return MH_DisableHook(_pTarget) == MH_STATUS.MH_OK && (_pOriginalStub is null ? true : MH_DisableHook(_pOriginalStub) == MH_STATUS.MH_OK);
		}

		public void Dispose() {
			if (_isDisposed)
				return;

			Disable();
			MH_RemoveHook(_pTarget);
			if (!(_pOriginalStub is null))
				MH_RemoveHook(_pOriginalStub);
			_isDisposed = true;
		}
	}
}
