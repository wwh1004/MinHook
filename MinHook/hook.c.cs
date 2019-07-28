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

using System.Runtime.InteropServices;
using System.Threading;
using static MinHooking.Buffer;
using static MinHooking.NativeMethods;
using static MinHooking.Trampoline;

namespace MinHooking {
	public static unsafe partial class MinHookNative {
		// Initial capacity of the HOOK_ENTRY buffer.
		private const byte INITIAL_HOOK_CAPACITY = 32;

		// Initial capacity of the thread IDs buffer.
		private const byte INITIAL_THREAD_CAPACITY = 128;

		// Special hook position values.
		private const uint INVALID_HOOK_POS = uint.MaxValue;
		private const uint ALL_HOOKS_POS = uint.MaxValue;

		// Freeze() action argument defines.
		private const byte ACTION_DISABLE = 0;
		private const byte ACTION_ENABLE = 1;
		private const byte ACTION_APPLY_QUEUED = 2;

		// Thread access rights for suspending/resuming threads.
		// THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT | THREAD_QUERY_INFORMATION | THREAD_SET_CONTEXT
		private const uint THREAD_ACCESS = 0x5a;

		// Hook information.
		//[StructLayout(LayoutKind.Sequential)]
		//private struct HOOK_ENTRY {
		//	public void* pTarget;        // Address of the target function.
		//	public void* pDetour;        // Address of the detour or relay function.
		//	public void* pTrampoline;    // Address of the trampoline function.
		//	public fixed byte backup[8]; // Original prologue of the target function.

		//	public byte patchAbove; // Uses the hot patch area.
		//	public byte isEnabled; // Enabled.
		//	public byte queueEnable; // Queued for enabling/disabling when != isEnabled.

		//	public uint nIP;         // Count of the instruction boundaries.
		//	public fixed byte oldIPs[8]; // Instruction boundaries of the target function.
		//	public fixed byte newIPs[8]; // Instruction boundaries of the trampoline function.
		//}
		[StructLayout(LayoutKind.Sequential)]
		private struct HOOK_ENTRY {
			public void* pTarget;        // Address of the target function.
			public void* pDetour;        // Address of the detour or relay function.
			public void* pTrampoline;    // Address of the trampoline function.
			public fixed byte backup[8]; // Original prologue of the target function.

			private uint _data1;
			// Uses the hot patch area
			public byte patchAbove {
				get => (byte)(_data1 & 1);
				set => _data1 = (_data1 & unchecked((uint)~1)) | (value & (uint)1);
			}
			// Enabled.
			public byte isEnabled {
				get => (byte)((_data1 & 2) >> 1);
				set => _data1 = (_data1 & unchecked((uint)~2)) | ((uint)(value << 1) & 2);
			}
			// Queued for enabling/disabling when != isEnabled.
			public byte queueEnable {
				get => (byte)((_data1 & 4) >> 2);
				set => _data1 = (_data1 & unchecked((uint)~4)) | ((uint)(value << 2) & 4);
			}

			private uint _data2;
			// Count of the instruction boundaries.
			public uint nIP {
				get => (byte)(_data2 & 0xf);
				set => _data2 = (_data2 & unchecked((uint)~0xf)) | (value & 0xf);
			}
			public fixed byte oldIPs[8]; // Instruction boundaries of the target function.
			public fixed byte newIPs[8]; // Instruction boundaries of the trampoline function.
		}

		// Suspended threads for Freeze()/Unfreeze().
		[StructLayout(LayoutKind.Sequential)]
		private struct FROZEN_THREADS {
			public uint* pItems;         // Data heap
			public uint capacity;        // Size of allocated data heap, items
			public uint size;            // Actual number of data items
		}

		//-------------------------------------------------------------------------
		// Global Variables:
		//-------------------------------------------------------------------------

		// Spin lock flag for EnterSpinLock()/LeaveSpinLock().
		private static volatile int g_isLocked = FALSE;

		// Private heap handle. If not 0, this library is initialized.
		private static void* g_hHeap;

		// Hook entries.
		[StructLayout(LayoutKind.Sequential)]
		private struct HOOKS {
			public HOOK_ENTRY* pItems;     // Data heap
			public uint capacity;   // Size of allocated data heap, items
			public uint size;       // Actual number of data items
		}

		private static HOOKS g_hooks;

		//-------------------------------------------------------------------------
		// Returns INVALID_HOOK_POS if not found.
		private static uint FindHookEntry(void* pTarget) {
			uint i;
			for (i = 0; i < g_hooks.size; ++i) {
				if (pTarget == g_hooks.pItems[i].pTarget)
					return i;
			}

			return INVALID_HOOK_POS;
		}

		//-------------------------------------------------------------------------
		private static HOOK_ENTRY* AddHookEntry() {
			if (g_hooks.pItems is null) {
				g_hooks.capacity = INITIAL_HOOK_CAPACITY;
				g_hooks.pItems = (HOOK_ENTRY*)HeapAlloc(
					g_hHeap, 0, g_hooks.capacity * (uint)sizeof(HOOK_ENTRY));
				if (g_hooks.pItems is null)
					return null;
			}
			else if (g_hooks.size >= g_hooks.capacity) {
				HOOK_ENTRY* p = (HOOK_ENTRY*)HeapReAlloc(
					g_hHeap, 0, g_hooks.pItems, g_hooks.capacity * 2 * (uint)sizeof(HOOK_ENTRY));
				if (p is null)
					return null;

				g_hooks.capacity *= 2;
				g_hooks.pItems = p;
			}

			return &g_hooks.pItems[g_hooks.size++];
		}

		//-------------------------------------------------------------------------
		private static void DeleteHookEntry(uint pos) {
			if (pos < g_hooks.size - 1)
				g_hooks.pItems[pos] = g_hooks.pItems[g_hooks.size - 1];

			g_hooks.size--;

			if (g_hooks.capacity / 2 >= INITIAL_HOOK_CAPACITY && g_hooks.capacity / 2 >= g_hooks.size) {
				HOOK_ENTRY* p = (HOOK_ENTRY*)HeapReAlloc(
					g_hHeap, 0, g_hooks.pItems, g_hooks.capacity / 2 * (uint)sizeof(HOOK_ENTRY));
				if (p is null)
					return;

				g_hooks.capacity /= 2;
				g_hooks.pItems = p;
			}
		}

		//-------------------------------------------------------------------------
		private static byte* FindOldIP(HOOK_ENTRY* pHook, byte* ip) {
			uint i;

			if (pHook->patchAbove != 0 && ip == ((byte*)pHook->pTarget - sizeof(JMP_REL)))
				return (byte*)pHook->pTarget;

			for (i = 0; i < pHook->nIP; ++i) {
				if (ip == ((byte*)pHook->pTrampoline + pHook->newIPs[i]))
					return (byte*)pHook->pTarget + pHook->oldIPs[i];
			}

			if (WIN64) {
				// Check relay function.
				if (ip == pHook->pDetour)
					return (byte*)pHook->pTarget;
			}

			return null;
		}

		//-------------------------------------------------------------------------
		private static byte* FindNewIP(HOOK_ENTRY* pHook, byte* ip) {
			uint i;
			for (i = 0; i < pHook->nIP; ++i) {
				if (ip == ((byte*)pHook->pTarget + pHook->oldIPs[i]))
					return (byte*)pHook->pTrampoline + pHook->newIPs[i];
			}

			return null;
		}

		//-------------------------------------------------------------------------
		private static void ProcessThreadIPs32(void* hThread, uint pos, uint action) {
			// If the thread suspended in the overwritten area,
			// move IP to the proper address.

			CONTEXT32 c = default(CONTEXT32);
			void* pIP;
			uint count;

			pIP = &c.Eip;

			c.ContextFlags = 0x00010001;
			// CONTEXT_CONTROL
			if (GetThreadContext(hThread, &c) == 0)
				return;

			if (pos == ALL_HOOKS_POS) {
				pos = 0;
				count = g_hooks.size;
			}
			else {
				count = pos + 1;
			}

			for (; pos < count; ++pos) {
				HOOK_ENTRY* pHook = &g_hooks.pItems[pos];
				uint enable;
				byte* ip;

				switch (action) {
				case ACTION_DISABLE:
					enable = FALSE;
					break;

				case ACTION_ENABLE:
					enable = TRUE;
					break;

				default: // ACTION_APPLY_QUEUED
					enable = pHook->queueEnable;
					break;
				}
				if (pHook->isEnabled == enable)
					continue;

				if (enable != 0)
					ip = FindNewIP(pHook, *(byte**)pIP);
				else
					ip = FindOldIP(pHook, *(byte**)pIP);

				if (!(ip is null)) {
					*(byte**)pIP = ip;
					SetThreadContext(hThread, &c);
				}
			}
		}

		private static void ProcessThreadIPs64(void* hThread, uint pos, uint action) {
			// If the thread suspended in the overwritten area,
			// move IP to the proper address.

			CONTEXT64 c = default(CONTEXT64);
			void* pIP;
			uint count;

			pIP = &c.Rip;

			c.ContextFlags = 0x00100001;
			// CONTEXT_CONTROL
			if (GetThreadContext(hThread, &c) == 0)
				return;

			if (pos == ALL_HOOKS_POS) {
				pos = 0;
				count = g_hooks.size;
			}
			else {
				count = pos + 1;
			}

			for (; pos < count; ++pos) {
				HOOK_ENTRY* pHook = &g_hooks.pItems[pos];
				uint enable;
				byte* ip;

				switch (action) {
				case ACTION_DISABLE:
					enable = FALSE;
					break;

				case ACTION_ENABLE:
					enable = TRUE;
					break;

				default: // ACTION_APPLY_QUEUED
					enable = pHook->queueEnable;
					break;
				}
				if (pHook->isEnabled == enable)
					continue;

				if (enable != 0)
					ip = FindNewIP(pHook, *(byte**)pIP);
				else
					ip = FindOldIP(pHook, *(byte**)pIP);

				if (!(ip is null)) {
					*(byte**)pIP = ip;
					SetThreadContext(hThread, &c);
				}
			}
		}

		//-------------------------------------------------------------------------
		private static void EnumerateThreads(FROZEN_THREADS* pThreads) {
			void* hSnapshot = CreateToolhelp32Snapshot(0x4, 0);
			// TH32CS_SNAPTHREAD
			if (hSnapshot != unchecked((void*)-1)) {
				// INVALID_HANDLE_VALUE
				THREADENTRY32 te;
				te.dwSize = (uint)sizeof(THREADENTRY32);
				if (Thread32First(hSnapshot, &te) != 0) {
					do {
						if (te.dwSize >= 0x10
							// FIELD_OFFSET(THREADENTRY32, th32OwnerProcessID) + sizeof(uint)
							&& te.th32OwnerProcessID == GetCurrentProcessId()
							&& te.th32ThreadID != GetCurrentThreadId()) {
							if (pThreads->pItems is null) {
								pThreads->capacity = INITIAL_THREAD_CAPACITY;
								pThreads->pItems
									= (uint*)HeapAlloc(g_hHeap, 0, pThreads->capacity * sizeof(uint));
								if (pThreads->pItems is null)
									break;
							}
							else if (pThreads->size >= pThreads->capacity) {
								uint* p = (uint*)HeapReAlloc(
									g_hHeap, 0, pThreads->pItems, pThreads->capacity * 2 * sizeof(uint));
								if (p is null)
									break;

								pThreads->capacity *= 2;
								pThreads->pItems = p;
							}
							pThreads->pItems[pThreads->size++] = te.th32ThreadID;
						}

						te.dwSize = (uint)sizeof(THREADENTRY32);
					} while (Thread32Next(hSnapshot, &te) != 0);
				}
				CloseHandle(hSnapshot);
			}
		}

		//-------------------------------------------------------------------------
		private static void Freeze(FROZEN_THREADS* pThreads, uint pos, uint action) {
#if !DEBUG
			pThreads->pItems = null;
			pThreads->capacity = 0;
			pThreads->size = 0;
			EnumerateThreads(pThreads);

			if (!(pThreads->pItems is null)) {
				uint i;
				for (i = 0; i < pThreads->size; ++i) {
					void* hThread = OpenThread(THREAD_ACCESS, FALSE, pThreads->pItems[i]);
					if (!(hThread is null)) {
						SuspendThread(hThread);
						if (WIN64)
							ProcessThreadIPs64(hThread, pos, action);
						else
							ProcessThreadIPs32(hThread, pos, action);
						CloseHandle(hThread);
					}
				}
			}
#endif
		}

		//-------------------------------------------------------------------------
		private static void Unfreeze(FROZEN_THREADS* pThreads) {
#if !DEBUG
			if (!(pThreads->pItems is null)) {
				uint i;
				for (i = 0; i < pThreads->size; ++i) {
					void* hThread = OpenThread(THREAD_ACCESS, FALSE, pThreads->pItems[i]);
					if (!(hThread is null)) {
						ResumeThread(hThread);
						CloseHandle(hThread);
					}
				}

				HeapFree(g_hHeap, 0, pThreads->pItems);
			}
#endif
		}

		//-------------------------------------------------------------------------
		private static MH_STATUS EnableHookLL(uint pos, uint enable) {
			HOOK_ENTRY* pHook = &g_hooks.pItems[pos];
			uint oldProtect;
			uint patchSize = (uint)sizeof(JMP_REL);
			byte* pPatchTarget = (byte*)pHook->pTarget;

			if (pHook->patchAbove != 0) {
				pPatchTarget -= sizeof(JMP_REL);
				patchSize += (uint)sizeof(JMP_REL_SHORT);
			}

			if (VirtualProtect(pPatchTarget, patchSize, 0x40, &oldProtect) == 0)
				// PAGE_EXECUTE_READWRITE
				return MH_STATUS.MH_ERROR_MEMORY_PROTECT;

			if (enable != 0) {
				JMP_REL* pJmp = (JMP_REL*)pPatchTarget;
				pJmp->opcode = 0xE9;
				pJmp->operand = (uint)((byte*)pHook->pDetour - (pPatchTarget + sizeof(JMP_REL)));

				if (pHook->patchAbove != 0) {
					JMP_REL_SHORT* pShortJmp = (JMP_REL_SHORT*)pHook->pTarget;
					pShortJmp->opcode = 0xEB;
					pShortJmp->operand = (byte)(0 - (sizeof(JMP_REL_SHORT) + sizeof(JMP_REL)));
				}
			}
			else {
				if (pHook->patchAbove != 0)
					memcpy(pPatchTarget, pHook->backup, (uint)sizeof(JMP_REL) + (uint)sizeof(JMP_REL_SHORT));
				else
					memcpy(pPatchTarget, pHook->backup, (uint)sizeof(JMP_REL));
			}

			VirtualProtect(pPatchTarget, patchSize, oldProtect, &oldProtect);

			// Just-in-case measure.
			FlushInstructionCache(GetCurrentProcess(), pPatchTarget, patchSize);

			pHook->isEnabled = enable != 0 ? TRUE : FALSE;
			pHook->queueEnable = enable != 0 ? TRUE : FALSE;

			return MH_STATUS.MH_OK;
		}

		//-------------------------------------------------------------------------
		private static MH_STATUS EnableAllHooksLL(uint enable) {
			MH_STATUS status = MH_STATUS.MH_OK;
			uint i, first = INVALID_HOOK_POS;

			for (i = 0; i < g_hooks.size; ++i) {
				if (g_hooks.pItems[i].isEnabled != enable) {
					first = i;
					break;
				}
			}

			if (first != INVALID_HOOK_POS) {
				FROZEN_THREADS threads;
				Freeze(&threads, ALL_HOOKS_POS, enable != 0 ? ACTION_ENABLE : ACTION_DISABLE);

				for (i = first; i < g_hooks.size; ++i) {
					if (g_hooks.pItems[i].isEnabled != enable) {
						status = EnableHookLL(i, enable);
						if (status != MH_STATUS.MH_OK)
							break;
					}
				}

				Unfreeze(&threads);
			}

			return status;
		}

		//-------------------------------------------------------------------------
		private static void EnterSpinLock() {
			uint spinCount = 0;

			// Wait until the flag is FALSE.
			while (Interlocked.CompareExchange(ref g_isLocked, TRUE, FALSE) != FALSE) {
				// No need to generate a memory barrier here, since InterlockedCompareExchange()
				// generates a full memory barrier itself.

				// Prevent the loop from being too busy.
				if (spinCount < 32)
					Thread.Sleep(0);
				else
					Thread.Sleep(1);

				spinCount++;
			}
		}

		//-------------------------------------------------------------------------
		private static void LeaveSpinLock() {
			// No need to generate a memory barrier here, since InterlockedExchange()
			// generates a full memory barrier itself.

			Interlocked.Exchange(ref g_isLocked, FALSE);
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_Initialize() {
			MH_STATUS status = MH_STATUS.MH_OK;

			EnterSpinLock();

			if (g_hHeap is null) {
				g_hHeap = HeapCreate(0, 0, 0);
				if (!(g_hHeap is null)) {
					// Initialize the internal function buffer.
					InitializeBuffer();
				}
				else {
					status = MH_STATUS.MH_ERROR_MEMORY_ALLOC;
				}
			}
			else {
				status = MH_STATUS.MH_ERROR_ALREADY_INITIALIZED;
			}

			LeaveSpinLock();

			return status;
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_Uninitialize() {
			MH_STATUS status = MH_STATUS.MH_OK;

			EnterSpinLock();

			if (!(g_hHeap is null)) {
				status = EnableAllHooksLL(FALSE);
				if (status == MH_STATUS.MH_OK) {
					// Free the internal function buffer.

					// HeapFree is actually not required, but some tools detect a false
					// memory leak without HeapFree.

					UninitializeBuffer();

					HeapFree(g_hHeap, 0, g_hooks.pItems);
					HeapDestroy(g_hHeap);

					g_hHeap = null;

					g_hooks.pItems = null;
					g_hooks.capacity = 0;
					g_hooks.size = 0;
				}
			}
			else {
				status = MH_STATUS.MH_ERROR_NOT_INITIALIZED;
			}

			LeaveSpinLock();

			return status;
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_CreateHook(void* pTarget, void* pDetour, void** ppOriginal) {
			MH_STATUS status = MH_STATUS.MH_OK;

			EnterSpinLock();

			if (!(g_hHeap is null)) {
				if (IsExecutableAddress(pTarget) != 0 && IsExecutableAddress(pDetour) != 0) {
					uint pos = FindHookEntry(pTarget);
					if (pos == INVALID_HOOK_POS) {
						void* pBuffer = AllocateBuffer(pTarget);
						if (!(pBuffer is null)) {
							if (WIN64) {
								TRAMPOLINE64 ct;

								ct.pTarget = pTarget;
								ct.pDetour = pDetour;
								ct.pTrampoline = pBuffer;
								if (CreateTrampolineFunction64(&ct) != 0) {
									HOOK_ENTRY* pHook = AddHookEntry();
									if (!(pHook is null)) {
										pHook->pTarget = ct.pTarget;
										pHook->pDetour = ct.pRelay;
										pHook->pTrampoline = ct.pTrampoline;
										pHook->patchAbove = ct.patchAbove != 0 ? TRUE : FALSE;
										pHook->isEnabled = FALSE;
										pHook->queueEnable = FALSE;
										pHook->nIP = ct.nIP;
										memcpy(pHook->oldIPs, ct.oldIPs, 8);
										memcpy(pHook->newIPs, ct.newIPs, 8);

										// Back up the target function.

										if (ct.patchAbove != 0) {
											memcpy(
												pHook->backup,
												(byte*)pTarget - sizeof(JMP_REL),
												(uint)sizeof(JMP_REL) + (uint)sizeof(JMP_REL_SHORT));
										}
										else {
											memcpy(pHook->backup, pTarget, (uint)sizeof(JMP_REL));
										}

										if (!(ppOriginal is null))
											*ppOriginal = pHook->pTrampoline;
									}
									else {
										status = MH_STATUS.MH_ERROR_MEMORY_ALLOC;
									}
								}
								else {
									status = MH_STATUS.MH_ERROR_UNSUPPORTED_FUNCTION;
								}

								if (status != MH_STATUS.MH_OK) {
									FreeBuffer(pBuffer);
								}
							}
							else {
								TRAMPOLINE32 ct;

								ct.pTarget = pTarget;
								ct.pDetour = pDetour;
								ct.pTrampoline = pBuffer;
								if (CreateTrampolineFunction32(&ct) != 0) {
									HOOK_ENTRY* pHook = AddHookEntry();
									if (!(pHook is null)) {
										pHook->pTarget = ct.pTarget;
										pHook->pDetour = ct.pDetour;
										pHook->pTrampoline = ct.pTrampoline;
										pHook->patchAbove = ct.patchAbove != 0 ? TRUE : FALSE;
										pHook->isEnabled = FALSE;
										pHook->queueEnable = FALSE;
										pHook->nIP = ct.nIP;
										memcpy(pHook->oldIPs, ct.oldIPs, 8);
										memcpy(pHook->newIPs, ct.newIPs, 8);

										// Back up the target function.

										if (ct.patchAbove != 0) {
											memcpy(
												pHook->backup,
												(byte*)pTarget - sizeof(JMP_REL),
												(uint)sizeof(JMP_REL) + (uint)sizeof(JMP_REL_SHORT));
										}
										else {
											memcpy(pHook->backup, pTarget, (uint)sizeof(JMP_REL));
										}

										if (!(ppOriginal is null))
											*ppOriginal = pHook->pTrampoline;
									}
									else {
										status = MH_STATUS.MH_ERROR_MEMORY_ALLOC;
									}
								}
								else {
									status = MH_STATUS.MH_ERROR_UNSUPPORTED_FUNCTION;
								}

								if (status != MH_STATUS.MH_OK) {
									FreeBuffer(pBuffer);
								}
							}
						}
						else {
							status = MH_STATUS.MH_ERROR_MEMORY_ALLOC;
						}
					}
					else {
						status = MH_STATUS.MH_ERROR_ALREADY_CREATED;
					}
				}
				else {
					status = MH_STATUS.MH_ERROR_NOT_EXECUTABLE;
				}
			}
			else {
				status = MH_STATUS.MH_ERROR_NOT_INITIALIZED;
			}

			LeaveSpinLock();

			return status;
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_RemoveHook(void* pTarget) {
			MH_STATUS status = MH_STATUS.MH_OK;

			EnterSpinLock();

			if (!(g_hHeap is null)) {
				uint pos = FindHookEntry(pTarget);
				if (pos != INVALID_HOOK_POS) {
					if (g_hooks.pItems[pos].isEnabled != 0) {
						FROZEN_THREADS threads;
						Freeze(&threads, pos, ACTION_DISABLE);

						status = EnableHookLL(pos, FALSE);

						Unfreeze(&threads);
					}

					if (status == MH_STATUS.MH_OK) {
						FreeBuffer(g_hooks.pItems[pos].pTrampoline);
						DeleteHookEntry(pos);
					}
				}
				else {
					status = MH_STATUS.MH_ERROR_NOT_CREATED;
				}
			}
			else {
				status = MH_STATUS.MH_ERROR_NOT_INITIALIZED;
			}

			LeaveSpinLock();

			return status;
		}

		//-------------------------------------------------------------------------
		private static MH_STATUS EnableHook(void* pTarget, uint enable) {
			MH_STATUS status = MH_STATUS.MH_OK;

			EnterSpinLock();

			if (!(g_hHeap is null)) {
				if (pTarget == MH_ALL_HOOKS) {
					status = EnableAllHooksLL(enable);
				}
				else {
					FROZEN_THREADS threads;
					uint pos = FindHookEntry(pTarget);
					if (pos != INVALID_HOOK_POS) {
						if (g_hooks.pItems[pos].isEnabled != enable) {
							Freeze(&threads, pos, ACTION_ENABLE);

							status = EnableHookLL(pos, enable);

							Unfreeze(&threads);
						}
						else {
							status = enable != 0 ? MH_STATUS.MH_ERROR_ENABLED : MH_STATUS.MH_ERROR_DISABLED;
						}
					}
					else {
						status = MH_STATUS.MH_ERROR_NOT_CREATED;
					}
				}
			}
			else {
				status = MH_STATUS.MH_ERROR_NOT_INITIALIZED;
			}

			LeaveSpinLock();

			return status;
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_EnableHook(void* pTarget) {
			return EnableHook(pTarget, TRUE);
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_DisableHook(void* pTarget) {
			return EnableHook(pTarget, FALSE);
		}

		//-------------------------------------------------------------------------
		private static MH_STATUS QueueHook(void* pTarget, uint queueEnable) {
			MH_STATUS status = MH_STATUS.MH_OK;

			EnterSpinLock();

			if (!(g_hHeap is null)) {
				if (pTarget == MH_ALL_HOOKS) {
					uint i;
					for (i = 0; i < g_hooks.size; ++i)
						g_hooks.pItems[i].queueEnable = queueEnable != 0 ? TRUE : FALSE;
				}
				else {
					uint pos = FindHookEntry(pTarget);
					if (pos != INVALID_HOOK_POS) {
						g_hooks.pItems[pos].queueEnable = queueEnable != 0 ? TRUE : FALSE;
					}
					else {
						status = MH_STATUS.MH_ERROR_NOT_CREATED;
					}
				}
			}
			else {
				status = MH_STATUS.MH_ERROR_NOT_INITIALIZED;
			}

			LeaveSpinLock();

			return status;
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_QueueEnableHook(void* pTarget) {
			return QueueHook(pTarget, TRUE);
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_QueueDisableHook(void* pTarget) {
			return QueueHook(pTarget, FALSE);
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_ApplyQueued() {
			MH_STATUS status = MH_STATUS.MH_OK;
			uint i, first = INVALID_HOOK_POS;

			EnterSpinLock();

			if (!(g_hHeap is null)) {
				for (i = 0; i < g_hooks.size; ++i) {
					if (g_hooks.pItems[i].isEnabled != g_hooks.pItems[i].queueEnable) {
						first = i;
						break;
					}
				}

				if (first != INVALID_HOOK_POS) {
					FROZEN_THREADS threads;
					Freeze(&threads, ALL_HOOKS_POS, ACTION_APPLY_QUEUED);

					for (i = first; i < g_hooks.size; ++i) {
						HOOK_ENTRY* pHook = &g_hooks.pItems[i];
						if (pHook->isEnabled != pHook->queueEnable) {
							status = EnableHookLL(i, pHook->queueEnable);
							if (status != MH_STATUS.MH_OK)
								break;
						}
					}

					Unfreeze(&threads);
				}
			}
			else {
				status = MH_STATUS.MH_ERROR_NOT_INITIALIZED;
			}

			LeaveSpinLock();

			return status;
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_CreateHookApiEx(string pszModule, string pszProcName, void* pDetour, void** ppOriginal, void** ppTarget) {
			void* hModule;
			void* pTarget;

			hModule = GetModuleHandle(pszModule);
			if (hModule is null)
				return MH_STATUS.MH_ERROR_MODULE_NOT_FOUND;

			pTarget = GetProcAddress(hModule, pszProcName);
			if (pTarget is null)
				return MH_STATUS.MH_ERROR_FUNCTION_NOT_FOUND;

			if (!(ppTarget is null))
				*ppTarget = pTarget;

			return MH_CreateHook(pTarget, pDetour, ppOriginal);
		}

		//-------------------------------------------------------------------------
		public static MH_STATUS MH_CreateHookApi(string pszModule, string pszProcName, void* pDetour, void** ppOriginal) {
			return MH_CreateHookApiEx(pszModule, pszProcName, pDetour, ppOriginal, null);
		}

		//-------------------------------------------------------------------------
		public static string MH_StatusToString(MH_STATUS status) {
			switch (status) {
			case MH_STATUS.MH_UNKNOWN:
				return nameof(MH_STATUS.MH_UNKNOWN);
			case MH_STATUS.MH_OK:
				return nameof(MH_STATUS.MH_OK);
			case MH_STATUS.MH_ERROR_ALREADY_INITIALIZED:
				return nameof(MH_STATUS.MH_ERROR_ALREADY_INITIALIZED);
			case MH_STATUS.MH_ERROR_NOT_INITIALIZED:
				return nameof(MH_STATUS.MH_ERROR_NOT_INITIALIZED);
			case MH_STATUS.MH_ERROR_ALREADY_CREATED:
				return nameof(MH_STATUS.MH_ERROR_NOT_CREATED);
			case MH_STATUS.MH_ERROR_NOT_CREATED:
				return nameof(MH_STATUS.MH_ERROR_NOT_CREATED);
			case MH_STATUS.MH_ERROR_ENABLED:
				return nameof(MH_STATUS.MH_ERROR_ENABLED);
			case MH_STATUS.MH_ERROR_DISABLED:
				return nameof(MH_STATUS.MH_ERROR_DISABLED);
			case MH_STATUS.MH_ERROR_NOT_EXECUTABLE:
				return nameof(MH_STATUS.MH_ERROR_NOT_EXECUTABLE);
			case MH_STATUS.MH_ERROR_UNSUPPORTED_FUNCTION:
				return nameof(MH_STATUS.MH_ERROR_UNSUPPORTED_FUNCTION);
			case MH_STATUS.MH_ERROR_MEMORY_ALLOC:
				return nameof(MH_STATUS.MH_ERROR_MEMORY_ALLOC);
			case MH_STATUS.MH_ERROR_MEMORY_PROTECT:
				return nameof(MH_STATUS.MH_ERROR_MEMORY_PROTECT);
			case MH_STATUS.MH_ERROR_MODULE_NOT_FOUND:
				return nameof(MH_STATUS.MH_ERROR_MODULE_NOT_FOUND);
			case MH_STATUS.MH_ERROR_FUNCTION_NOT_FOUND:
				return nameof(MH_STATUS.MH_ERROR_FUNCTION_NOT_FOUND);
			default:
				return "(unknown)";
			}
		}
	}
}
