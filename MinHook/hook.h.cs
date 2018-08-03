/*
 *  MinHook - The Minimalistic API Hooking Library for x64/x86
 *  Copyright (C) 2009-2017 Tsuda Kageyu.
 *  All rights reserved.
 *
 *  Redistribution and use in source and binary forms, with or without
 *  modification, are permitted provided that the following conditions
 *  are met:
 *
 *   1. Redistributions of source code must retain the above copyright
 *      notice, this list of conditions and the following disclaimer.
 *   2. Redistributions in binary form must reproduce the above copyright
 *      notice, this list of conditions and the following disclaimer in the
 *      documentation and/or other materials provided with the distribution.
 *
 *  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 *  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
 *  TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 *  PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER
 *  OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 *  EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 *  PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 *  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 *  LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 *  NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 *  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

namespace MinHooks {
	public static unsafe partial class MinHookNative {
		// MinHook Error Codes.
		public enum MH_STATUS {
			// Unknown error. Should not be returned.
			MH_UNKNOWN = -1,

			// Successful.
			MH_OK = 0,

			// MinHook is already initialized.
			MH_ERROR_ALREADY_INITIALIZED,

			// MinHook is not initialized yet, or already uninitialized.
			MH_ERROR_NOT_INITIALIZED,

			// The hook for the specified target function is already created.
			MH_ERROR_ALREADY_CREATED,

			// The hook for the specified target function is not created yet.
			MH_ERROR_NOT_CREATED,

			// The hook for the specified target function is already enabled.
			MH_ERROR_ENABLED,

			// The hook for the specified target function is not enabled yet, or already
			// disabled.
			MH_ERROR_DISABLED,

			// The specified pointer is invalid. It points the address of non-allocated
			// and/or non-executable region.
			MH_ERROR_NOT_EXECUTABLE,

			// The specified target function cannot be hooked.
			MH_ERROR_UNSUPPORTED_FUNCTION,

			// Failed to allocate memory.
			MH_ERROR_MEMORY_ALLOC,

			// Failed to change the memory protection.
			MH_ERROR_MEMORY_PROTECT,

			// The specified module is not loaded.
			MH_ERROR_MODULE_NOT_FOUND,

			// The specified function is not found.
			MH_ERROR_FUNCTION_NOT_FOUND
		}

		// Can be passed as a parameter to MH_EnableHook, MH_DisableHook,
		// MH_QueueEnableHook or MH_QueueDisableHook.
		private static readonly void* MH_ALL_HOOKS = null;
	}
}
