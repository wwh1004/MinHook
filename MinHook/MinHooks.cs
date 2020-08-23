using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using static MinHooking.Hook;

namespace MinHooking {
	public unsafe interface IMinHook : IDisposable {
		void* TargetAddress { get; }

		bool Enable();

		bool Disable();
	}

	public sealed unsafe class MinHook : IMinHook {
		private readonly void* _pTarget;
		private bool _isDisposed;

		public void* TargetAddress => _pTarget;

		static MinHook() {
			MH_Initialize();
		}

		internal MinHook(void* pTarget) {
			if (pTarget == null)
				throw new ArgumentNullException(nameof(pTarget));

			_pTarget = pTarget;
		}

		internal static MinHook Create(void* pTarget, void* pDetour, out void* pOriginal) {
			fixed (void** p = &pOriginal)
				return MH_CreateHook(pTarget, pDetour, p) == MH_STATUS.MH_OK ? new MinHook(pTarget) : null;
		}

		public bool Enable() {
			return MH_EnableHook(_pTarget) == MH_STATUS.MH_OK;
		}

		public bool Disable() {
			return MH_DisableHook(_pTarget) == MH_STATUS.MH_OK;
		}

		public void Dispose() {
			if (_isDisposed)
				return;

			Disable();
			MH_RemoveHook(_pTarget);
			_isDisposed = true;
		}
	}

	public sealed unsafe class MinHookManaged : IMinHook {
		private readonly IMinHook _targetHook;
		private readonly IMinHook _originalStubHook;
		private bool _isDisposed;

		public void* TargetAddress => _targetHook.TargetAddress;

		internal MinHookManaged(IMinHook targetHook, IMinHook originalStubHook) {
			if (targetHook is null)
				throw new ArgumentNullException(nameof(targetHook));
			if (originalStubHook is null)
				throw new ArgumentNullException(nameof(originalStubHook));

			_targetHook = targetHook;
			_originalStubHook = originalStubHook;
		}

		internal static MinHookManaged Create(void* pTarget, void* pDetour, void* pOriginalStub) {
			var targetHook = MinHookFactory.Create(pTarget, pDetour, out void* pOriginal);
			if (targetHook is null)
				return null;
			var originalStubHook = MinHookFactory.Create(pOriginalStub, pOriginal);
			if (originalStubHook is null) {
				targetHook.Dispose();
				return null;
			}
			return new MinHookManaged(targetHook, originalStubHook);
		}

		public bool Enable() {
			return _originalStubHook.Enable() && _targetHook.Enable();
		}

		public bool Disable() {
			return _targetHook.Disable() && _originalStubHook.Disable();
		}

		public void Dispose() {
			if (_isDisposed)
				return;

			Disable();
			_targetHook.Dispose();
			_originalStubHook.Dispose();
			_isDisposed = true;
		}
	}

	public static unsafe class MinHookFactory {
		public static IMinHook Create(void* pTarget, void* pDetour) {
			return Create(pTarget, pDetour, out _);
		}

		public static IMinHook Create(void* pTarget, void* pDetour, out void* pOriginal) {
			if (pTarget == null)
				throw new ArgumentNullException(nameof(pTarget));
			if (pDetour == null)
				throw new ArgumentNullException(nameof(pDetour));

			return MinHook.Create(pTarget, pDetour, out pOriginal);
		}

		public static IMinHook Create(MethodBase target, MethodBase detour) {
			if (target is null)
				throw new ArgumentNullException(nameof(target));
			if (detour is null)
				throw new ArgumentNullException(nameof(detour));

			return Create(GetMethodAddress(target), GetMethodAddress(detour));
		}

		public static IMinHook Create(MethodBase target, MethodBase detour, MethodBase originalStub) {
			if (target is null)
				throw new ArgumentNullException(nameof(target));
			if (detour is null)
				throw new ArgumentNullException(nameof(detour));
			if (originalStub is null)
				throw new ArgumentNullException(nameof(originalStub));

			return Create(GetMethodAddress(target), GetMethodAddress(detour), GetMethodAddress(originalStub));
		}

		public static IMinHook Create(void* pTarget, void* pDetour, void* pOriginalStub) {
			if (pTarget == null)
				throw new ArgumentNullException(nameof(pTarget));
			if (pDetour == null)
				throw new ArgumentNullException(nameof(pDetour));
			if (pOriginalStub == null)
				throw new ArgumentNullException(nameof(pOriginalStub));

			return MinHookManaged.Create(pTarget, pDetour, pOriginalStub);
		}

		private static void* GetMethodAddress(MethodBase method) {
			if (method is null)
				throw new ArgumentNullException(nameof(method));

			RuntimeHelpers.PrepareMethod(method.MethodHandle);
			return (void*)method.MethodHandle.GetFunctionPointer();
		}
	}
}
