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

using System;
using System.Runtime.InteropServices;
using static MinHooking.NativeMethods;

namespace MinHooking {
	internal static unsafe partial class Buffer {
		// Size of each memory block. (= page size of VirtualAlloc)
		private const ushort MEMORY_BLOCK_SIZE = 0x1000;

		// Max range for seeking a memory block. (= 1024MB)
		private const uint MAX_MEMORY_RANGE = 0x40000000;

		// Memory protection flags to check the executable address.
		// PAGE_EXECUTE | PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE | PAGE_EXECUTE_WRITECOPY
		private const uint PAGE_EXECUTE_FLAGS = 0xf0;

		// Memory slot.
		[StructLayout(LayoutKind.Explicit)]
		private struct MEMORY_SLOT {
			[FieldOffset(0)]
			private readonly DATA data;
			[FieldOffset(0)]
			public MEMORY_SLOT* pNext;
			[FieldOffset(0)]
			public fixed byte buffer[1];

			[StructLayout(LayoutKind.Sequential)]
			private struct DATA {
				private readonly IntPtr data1;
				private readonly IntPtr data2;
				private readonly IntPtr data3;
				private readonly IntPtr data4;
				private readonly IntPtr data5;
				private readonly IntPtr data6;
				private readonly IntPtr data7;
				private readonly IntPtr data8;
			}
		}

		// Memory block info. Placed at the head of each block.
		[StructLayout(LayoutKind.Sequential)]
		private struct MEMORY_BLOCK {
			public MEMORY_BLOCK* pNext;
			public MEMORY_SLOT* pFree;         // First element of the free slot list.
			public uint usedCount;
		}

		//-------------------------------------------------------------------------
		// Global Variables:
		//-------------------------------------------------------------------------

		// First element of the memory block list.
		private static MEMORY_BLOCK* g_pMemoryBlocks;

		//-------------------------------------------------------------------------
		public static void InitializeBuffer() {
			// Nothing to do for now.
		}

		//-------------------------------------------------------------------------
		public static void UninitializeBuffer() {
			MEMORY_BLOCK* pBlock = g_pMemoryBlocks;
			g_pMemoryBlocks = null;

			while (!(pBlock == null)) {
				MEMORY_BLOCK* pNext = pBlock->pNext;
				VirtualFree(pBlock, 0, 0x8000);
				// MEM_RELEASE
				pBlock = pNext;
			}
		}

		//-------------------------------------------------------------------------
		private static void* FindPrevFreeRegion(void* pAddress, void* pMinAddr, uint dwAllocationGranularity) {
			byte* tryAddr = (byte*)pAddress;

			// Round down to the allocation granularity.
			tryAddr -= (ulong)tryAddr % dwAllocationGranularity;

			// Start from the previous allocation granularity multiply.
			tryAddr -= dwAllocationGranularity;

			while (tryAddr >= pMinAddr) {
				MEMORY_BASIC_INFORMATION mbi;
				if (VirtualQuery((void*)tryAddr, &mbi, (uint)sizeof(MEMORY_BASIC_INFORMATION)) == null)
					break;

				if (mbi.State == 0x00010000)
					// MEM_FREE
					return (void*)tryAddr;

				if ((ulong)mbi.AllocationBase < dwAllocationGranularity)
					break;

				tryAddr = mbi.AllocationBase - dwAllocationGranularity;
			}

			return null;
		}

		//-------------------------------------------------------------------------
		private static void* FindNextFreeRegion(void* pAddress, void* pMaxAddr, uint dwAllocationGranularity) {
			byte* tryAddr = (byte*)pAddress;

			// Round down to the allocation granularity.
			tryAddr -= (ulong)tryAddr % dwAllocationGranularity;

			// Start from the next allocation granularity multiply.
			tryAddr += dwAllocationGranularity;

			while (tryAddr <= pMaxAddr) {
				MEMORY_BASIC_INFORMATION mbi;
				if (VirtualQuery((void*)tryAddr, &mbi, (uint)sizeof(MEMORY_BASIC_INFORMATION)) == null)
					break;

				if (mbi.State == 0x00010000)
					// MEM_FREE
					return (void*)tryAddr;

				tryAddr = mbi.BaseAddress + (ulong)mbi.RegionSize;

				// Round up to the next allocation granularity.
				tryAddr += dwAllocationGranularity - 1;
				tryAddr -= (ulong)tryAddr % dwAllocationGranularity;
			}

			return null;
		}

		//-------------------------------------------------------------------------
		private static MEMORY_BLOCK* GetMemoryBlock(void* pOrigin) {
			MEMORY_BLOCK* pBlock;
			byte* minAddr = null;
			byte* maxAddr = null;
			SYSTEM_INFO si = default;

			if (WIN64) {
				GetSystemInfo(&si);
				minAddr = si.lpMinimumApplicationAddress;
				maxAddr = si.lpMaximumApplicationAddress;

				// pOrigin ± 512MB
				if ((ulong)pOrigin > MAX_MEMORY_RANGE && minAddr < (byte*)pOrigin - MAX_MEMORY_RANGE)
					minAddr = (byte*)pOrigin - MAX_MEMORY_RANGE;

				if (maxAddr > (byte*)pOrigin + MAX_MEMORY_RANGE)
					maxAddr = (byte*)pOrigin + MAX_MEMORY_RANGE;

				// Make room for MEMORY_BLOCK_SIZE bytes.
				maxAddr -= MEMORY_BLOCK_SIZE - 1;
			}

			// Look the registered blocks for a reachable one.
			for (pBlock = g_pMemoryBlocks; !(pBlock == null); pBlock = pBlock->pNext) {
				if (WIN64) {
					// Ignore the blocks too far.
					if ((byte*)pBlock < minAddr || (byte*)pBlock >= maxAddr)
						continue;
				}
				// The block has at least one unused slot.
				if (!(pBlock->pFree == null))
					return pBlock;
			}

			if (WIN64) {
				// Alloc a new block above if not found.
				{
					void* pAlloc = pOrigin;
					while (pAlloc >= minAddr) {
						pAlloc = FindPrevFreeRegion(pAlloc, (void*)minAddr, si.dwAllocationGranularity);
						if (pAlloc == null)
							break;

						pBlock = (MEMORY_BLOCK*)VirtualAlloc(
							pAlloc, MEMORY_BLOCK_SIZE, 0x3000, 0x40);
						// MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE
						if (!(pBlock == null))
							break;
					}
				}

				// Alloc a new block below if not found.
				if (pBlock == null) {
					void* pAlloc = pOrigin;
					while (pAlloc <= maxAddr) {
						pAlloc = FindNextFreeRegion(pAlloc, (void*)maxAddr, si.dwAllocationGranularity);
						if (pAlloc == null)
							break;

						pBlock = (MEMORY_BLOCK*)VirtualAlloc(
							pAlloc, MEMORY_BLOCK_SIZE, 0x3000, 0x40);
						// MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE
						if (!(pBlock == null))
							break;
					}
				}
			}
			else {
				// In x86 mode, a memory block can be placed anywhere.
				pBlock = (MEMORY_BLOCK*)VirtualAlloc(
					null, MEMORY_BLOCK_SIZE, 0x3000, 0x40);
				// MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE
			}

			if (!(pBlock == null)) {
				// Build a linked list of all the slots.
				MEMORY_SLOT* pSlot = (MEMORY_SLOT*)pBlock + 1;
				pBlock->pFree = null;
				pBlock->usedCount = 0;
				do {
					pSlot->pNext = pBlock->pFree;
					pBlock->pFree = pSlot;
					pSlot++;
				} while ((byte*)pSlot - (byte*)pBlock <= MEMORY_BLOCK_SIZE - MEMORY_SLOT_SIZE);

				pBlock->pNext = g_pMemoryBlocks;
				g_pMemoryBlocks = pBlock;
			}

			return pBlock;
		}

		//-------------------------------------------------------------------------
		public static void* AllocateBuffer(void* pOrigin) {
			MEMORY_SLOT* pSlot;
			MEMORY_BLOCK* pBlock = GetMemoryBlock(pOrigin);
			if (pBlock == null)
				return null;

			// Remove an unused slot from the list.
			pSlot = pBlock->pFree;
			pBlock->pFree = pSlot->pNext;
			pBlock->usedCount++;
			return pSlot;
		}

		//-------------------------------------------------------------------------
		public static void FreeBuffer(void* pBuffer) {
			MEMORY_BLOCK* pBlock = g_pMemoryBlocks;
			MEMORY_BLOCK* pPrev = null;
			byte* pTargetBlock = (byte*)((ulong)pBuffer / MEMORY_BLOCK_SIZE * MEMORY_BLOCK_SIZE);

			while (!(pBlock == null)) {
				if ((byte*)pBlock == pTargetBlock) {
					MEMORY_SLOT* pSlot = (MEMORY_SLOT*)pBuffer;
					// Restore the released slot to the list.
					pSlot->pNext = pBlock->pFree;
					pBlock->pFree = pSlot;
					pBlock->usedCount--;

					// Free if unused.
					if (pBlock->usedCount == 0) {
						if (!(pPrev == null))
							pPrev->pNext = pBlock->pNext;
						else
							g_pMemoryBlocks = pBlock->pNext;

						VirtualFree(pBlock, 0, 0x8000);
						// MEM_RELEASE
					}

					break;
				}

				pPrev = pBlock;
				pBlock = pBlock->pNext;
			}
		}

		//-------------------------------------------------------------------------
		public static uint IsExecutableAddress(void* pAddress) {
			MEMORY_BASIC_INFORMATION mi;
			VirtualQuery(pAddress, &mi, (uint)sizeof(MEMORY_BASIC_INFORMATION));

			return (mi.State == 0x1000 && (mi.Protect & PAGE_EXECUTE_FLAGS) != 0) ? TRUE : FALSE;
			// MEM_COMMIT
		}
	}
}
