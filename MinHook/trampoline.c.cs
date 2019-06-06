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

using MinHooks.Hde;
using static MinHooks.Buffer;
using static MinHooks.Hde.Hde32;
using static MinHooks.Hde.Hde64;
using static MinHooks.NativeMethods;

namespace MinHooks {
	internal static unsafe partial class Trampoline {
		private static uint HDE_DISASM(void* code, void* hs) {
			return WIN64 ? hde64_disasm(code, (hde64s*)hs) : hde32_disasm(code, (hde32s*)hs);
		}

		// Maximum size of a trampoline function.
		private static byte TRAMPOLINE_MAX_SIZE = WIN64 ? (byte)(MEMORY_SLOT_SIZE - sizeof(JMP_ABS)) : MEMORY_SLOT_SIZE;

		//-------------------------------------------------------------------------
		static uint IsCodePadding(byte* pInst, uint size) {
			uint i;

			if (pInst[0] != 0x00 && pInst[0] != 0x90 && pInst[0] != 0xCC)
				return FALSE;

			for (i = 1; i < size; ++i) {
				if (pInst[i] != pInst[0])
					return FALSE;
			}
			return TRUE;
		}

		//-------------------------------------------------------------------------
		public static uint CreateTrampolineFunction32(TRAMPOLINE32* ct) {
			CALL_REL call = new CALL_REL {
				opcode = 0xE8,                  // E8 xxxxxxxx: CALL +5+xxxxxxxx
				operand = 0x00000000            // Relative destination address
			};
			JMP_REL jmp = new JMP_REL {
				opcode = 0xE9,                  // E9 xxxxxxxx: JMP +5+xxxxxxxx
				operand = 0x00000000            // Relative destination address
			};
			JCC_REL jcc = new JCC_REL {
				opcode0 = 0x0F, opcode1 = 0x80, // 0F8* xxxxxxxx: J** +6+xxxxxxxx
				operand = 0x00000000            // Relative destination address
			};


			byte oldPos = 0;
			byte newPos = 0;
			byte* jmpDest = (byte*)0;           // Destination address of an internal jump.
			uint finished = FALSE;              // Is the function completed?

			ct->patchAbove = FALSE;
			ct->nIP = 0;

			do {
				hde64s hs;
				uint copySize;
				void* pCopySrc;
				byte* pOldInst = (byte*)ct->pTarget + oldPos;
				byte* pNewInst = (byte*)ct->pTrampoline + newPos;

				copySize = HDE_DISASM((void*)pOldInst, &hs);
				if ((hs.flags & Hde32.F_ERROR) != 0)
					return FALSE;

				pCopySrc = (void*)pOldInst;
				if (oldPos >= sizeof(JMP_REL)) {
					// The trampoline function is long enough.
					// Complete the function with the jump to the target function.
					jmp.operand = (uint)(pOldInst - (pNewInst + sizeof(JMP_REL)));
					pCopySrc = &jmp;
					copySize = (uint)sizeof(JMP_REL);

					finished = TRUE;
				}
				else if (hs.opcode == 0xE8) {
					// Direct relative CALL
					byte* dest = pOldInst + hs.len + (int)hs.imm.imm32;
					call.operand = (uint)(dest - (pNewInst + sizeof(CALL_REL)));
					pCopySrc = &call;
					copySize = (uint)sizeof(CALL_REL);
				}
				else if ((hs.opcode & 0xFD) == 0xE9) {
					// Direct relative JMP (EB or E9)
					byte* dest = pOldInst + hs.len;

					if (hs.opcode == 0xEB) // isShort jmp
						dest += hs.imm.imm8;
					else
						dest += (int)hs.imm.imm32;

					// Simply copy an internal jump.
					if (ct->pTarget <= dest
						&& dest < ((byte*)ct->pTarget + sizeof(JMP_REL))) {
						if (jmpDest < dest)
							jmpDest = dest;
					}
					else {
						jmp.operand = (uint)(dest - (pNewInst + sizeof(JMP_REL)));
						pCopySrc = &jmp;
						copySize = (uint)sizeof(JMP_REL);

						// Exit the function If it is not in the branch
						finished = pOldInst >= jmpDest ? TRUE : FALSE;
					}
				}
				else if ((hs.opcode & 0xF0) == 0x70
					|| (hs.opcode & 0xFC) == 0xE0
					|| (hs.opcode2 & 0xF0) == 0x80) {
					// Direct relative Jcc
					byte* dest = pOldInst + hs.len;

					if ((hs.opcode & 0xF0) == 0x70      // Jcc
						|| (hs.opcode & 0xFC) == 0xE0)  // LOOPNZ/LOOPZ/LOOP/JECXZ
						dest += hs.imm.imm8;
					else
						dest += (int)hs.imm.imm32;

					// Simply copy an internal jump.
					if (ct->pTarget <= dest
						&& dest < ((byte*)ct->pTarget + sizeof(JMP_REL))) {
						if (jmpDest < dest)
							jmpDest = dest;
					}
					else if ((hs.opcode & 0xFC) == 0xE0) {
						// LOOPNZ/LOOPZ/LOOP/JCXZ/JECXZ to the outside are not supported.
						return FALSE;
					}
					else {
						byte cond = (byte)((hs.opcode != 0x0F ? hs.opcode : hs.opcode2) & 0x0F);
						jcc.opcode1 = (byte)(0x80 | cond);
						jcc.operand = (uint)(dest - (pNewInst + sizeof(JCC_REL)));
						pCopySrc = &jcc;
						copySize = (uint)sizeof(JCC_REL);
					}
				}
				else if ((hs.opcode & 0xFE) == 0xC2) {
					// RET (C2 or C3)

					// Complete the function if not in a branch.
					finished = pOldInst >= jmpDest ? TRUE : FALSE;
				}

				// Can't alter the instruction length in a branch.
				if (pOldInst < jmpDest && copySize != hs.len)
					return FALSE;

				// Trampoline function is too large.
				if ((newPos + copySize) > TRAMPOLINE_MAX_SIZE)
					return FALSE;

				// Trampoline function has too many instructions.
				if (ct->nIP >= 8)
					return FALSE;

				ct->oldIPs[ct->nIP] = oldPos;
				ct->newIPs[ct->nIP] = newPos;
				ct->nIP++;

				// Avoid using memcpy to reduce the footprint.
				memcpy((byte*)ct->pTrampoline + newPos, pCopySrc, copySize);
				newPos += (byte)copySize;
				oldPos += hs.len;
			}
			while (finished == 0);

			// Is there enough place for a long jump?
			if (oldPos < sizeof(JMP_REL)
				&& IsCodePadding((byte*)ct->pTarget + oldPos, (uint)sizeof(JMP_REL) - oldPos) == 0) {
				// Is there enough place for a short jump?
				if (oldPos < sizeof(JMP_REL_SHORT)
					&& IsCodePadding((byte*)ct->pTarget + oldPos, (uint)sizeof(JMP_REL_SHORT) - oldPos) == 0) {
					return FALSE;
				}

				// Can we place the long jump above the function?
				if (IsExecutableAddress((byte*)ct->pTarget - sizeof(JMP_REL)) == 0)
					return FALSE;

				if (IsCodePadding((byte*)ct->pTarget - sizeof(JMP_REL), (uint)sizeof(JMP_REL)) == 0)
					return FALSE;

				ct->patchAbove = TRUE;
			}

			return TRUE;
		}

		public static uint CreateTrampolineFunction64(TRAMPOLINE64* ct) {
			CALL_ABS call = new CALL_ABS {
				opcode0 = 0xFF, opcode1 = 0x15, dummy0 = 0x00000002, // FF15 00000002: CALL [RIP+8]
				dummy1 = 0xEB, dummy2 = 0x08,                        // EB 08:         JMP +10
				address = 0x0000000000000000                         // Absolute destination address
			};
			JMP_ABS jmp = new JMP_ABS {
				opcode0 = 0xFF, opcode1 = 0x25, dummy = 0x00000000,  // FF25 00000000: JMP [RIP+6]
				address = 0x0000000000000000                         // Absolute destination address
			};
			JCC_ABS jcc = new JCC_ABS {
				opcode = 0x70, dummy0 = 0x0E,                        // 7* 0E:         J** +16
				dummy1 = 0xFF, dummy2 = 0x25, dummy3 = 0x00000000,   // FF25 00000000: JMP [RIP+6]
				address = 0x0000000000000000                         // Absolute destination address
			};


			byte oldPos = 0;
			byte newPos = 0;
			byte* jmpDest = (byte*)0;                                // Destination address of an internal jump.
			uint finished = FALSE;                                   // Is the function completed?
			byte* instBuf = stackalloc byte[16];

			ct->patchAbove = FALSE;
			ct->nIP = 0;

			do {
				hde64s hs;
				uint copySize;
				void* pCopySrc;
				byte* pOldInst = (byte*)ct->pTarget + oldPos;
				byte* pNewInst = (byte*)ct->pTrampoline + newPos;

				copySize = HDE_DISASM((void*)pOldInst, &hs);
				if ((hs.flags & Hde64.F_ERROR) != 0)
					return FALSE;

				pCopySrc = (void*)pOldInst;
				if (oldPos >= sizeof(JMP_REL)) {
					// The trampoline function is long enough.
					// Complete the function with the jump to the target function.
					jmp.address = (ulong)pOldInst;
					pCopySrc = &jmp;
					copySize = (uint)sizeof(JMP_ABS);

					finished = TRUE;
				}
				else if ((hs.modrm & 0xC7) == 0x05) {
					// Instructions using RIP relative addressing. (ModR/M = 00???101B)

					// Modify the RIP relative address.
					uint* pRelAddr;

					// Avoid using memcpy to reduce the footprint.
					memcpy(instBuf, pOldInst, copySize);
					pCopySrc = instBuf;

					// Relative address is stored at (instruction length - immediate value length - 4).
					pRelAddr = (uint*)(instBuf + hs.len - ((hs.flags & 0x3C) >> 2) - 4);
					*pRelAddr
						= (uint)(pOldInst + hs.len + (int)hs.disp.disp32 - (pNewInst + hs.len));

					// Complete the function if JMP (FF /4).
					if (hs.opcode == 0xFF && hs.modrm_reg == 4)
						finished = TRUE;
				}
				else if (hs.opcode == 0xE8) {
					// Direct relative CALL
					byte* dest = pOldInst + hs.len + (int)hs.imm.imm32;
					call.address = (ulong)dest;
					pCopySrc = &call;
					copySize = (uint)sizeof(CALL_ABS);
				}
				else if ((hs.opcode & 0xFD) == 0xE9) {
					// Direct relative JMP (EB or E9)
					byte* dest = pOldInst + hs.len;

					if (hs.opcode == 0xEB) // isShort jmp
						dest += hs.imm.imm8;
					else
						dest += (int)hs.imm.imm32;

					// Simply copy an internal jump.
					if (ct->pTarget <= dest
						&& dest < ((byte*)ct->pTarget + sizeof(JMP_REL))) {
						if (jmpDest < dest)
							jmpDest = dest;
					}
					else {
						jmp.address = (ulong)dest;
						pCopySrc = &jmp;
						copySize = (uint)sizeof(JMP_ABS);

						// Exit the function If it is not in the branch
						finished = pOldInst >= jmpDest ? TRUE : FALSE;
					}
				}
				else if ((hs.opcode & 0xF0) == 0x70
					|| (hs.opcode & 0xFC) == 0xE0
					|| (hs.opcode2 & 0xF0) == 0x80) {
					// Direct relative Jcc
					byte* dest = pOldInst + hs.len;

					if ((hs.opcode & 0xF0) == 0x70      // Jcc
						|| (hs.opcode & 0xFC) == 0xE0)  // LOOPNZ/LOOPZ/LOOP/JECXZ
						dest += hs.imm.imm8;
					else
						dest += (int)hs.imm.imm32;

					// Simply copy an internal jump.
					if (ct->pTarget <= dest
						&& dest < ((byte*)ct->pTarget + sizeof(JMP_REL))) {
						if (jmpDest < dest)
							jmpDest = dest;
					}
					else if ((hs.opcode & 0xFC) == 0xE0) {
						// LOOPNZ/LOOPZ/LOOP/JCXZ/JECXZ to the outside are not supported.
						return FALSE;
					}
					else {
						byte cond = (byte)((hs.opcode != 0x0F ? hs.opcode : hs.opcode2) & 0x0F);
						// Invert the condition in x64 mode to simplify the conditional jump logic.
						jcc.opcode = (byte)(0x71 ^ cond);
						jcc.address = (ulong)dest;
						pCopySrc = &jcc;
						copySize = (uint)sizeof(JCC_ABS);
					}
				}
				else if ((hs.opcode & 0xFE) == 0xC2) {
					// RET (C2 or C3)

					// Complete the function if not in a branch.
					finished = pOldInst >= jmpDest ? TRUE : FALSE;
				}

				// Can't alter the instruction length in a branch.
				if (pOldInst < jmpDest && copySize != hs.len)
					return FALSE;

				// Trampoline function is too large.
				if ((newPos + copySize) > TRAMPOLINE_MAX_SIZE)
					return FALSE;

				// Trampoline function has too many instructions.
				if (ct->nIP >= 8)
					return FALSE;

				ct->oldIPs[ct->nIP] = oldPos;
				ct->newIPs[ct->nIP] = newPos;
				ct->nIP++;

				// Avoid using memcpy to reduce the footprint.
				memcpy((byte*)ct->pTrampoline + newPos, pCopySrc, copySize);
				newPos += (byte)copySize;
				oldPos += hs.len;
			}
			while (finished == 0);

			// Is there enough place for a long jump?
			if (oldPos < sizeof(JMP_REL)
				&& IsCodePadding((byte*)ct->pTarget + oldPos, (uint)sizeof(JMP_REL) - oldPos) == 0) {
				// Is there enough place for a short jump?
				if (oldPos < sizeof(JMP_REL_SHORT)
					&& IsCodePadding((byte*)ct->pTarget + oldPos, (uint)sizeof(JMP_REL_SHORT) - oldPos) == 0) {
					return FALSE;
				}

				// Can we place the long jump above the function?
				if (IsExecutableAddress((byte*)ct->pTarget - sizeof(JMP_REL)) == 0)
					return FALSE;

				if (IsCodePadding((byte*)ct->pTarget - sizeof(JMP_REL), (uint)sizeof(JMP_REL)) == 0)
					return FALSE;

				ct->patchAbove = TRUE;
			}

			// Create a relay function.
			jmp.address = (ulong)ct->pDetour;

			ct->pRelay = (byte*)ct->pTrampoline + newPos;
			memcpy(ct->pRelay, &jmp, (uint)sizeof(JMP_ABS));

			return TRUE;
		}
	}
}
