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

namespace MinHooking {
	internal static unsafe partial class Trampoline {
		// Structs for writing x86/x64 instructions.

		// 8-bit relative jump.
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct JMP_REL_SHORT {
			public byte opcode;      // EB xx: JMP +2+xx
			public byte operand;
		}

		// 32-bit direct relative jump
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct JMP_REL {
			public byte opcode;      // E9 xxxxxxxx: JMP +5+xxxxxxxx
			public uint operand;     // Relative destination address
		}

		// 64-bit indirect absolute jump.
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct JMP_ABS {
			public byte opcode0;     // FF25 00000000: JMP [+6]
			public byte opcode1;
			public uint dummy;
			public ulong address;     // Absolute destination address
		}

		// 32-bit direct relative call.
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct CALL_REL {
			public byte opcode;      // E8 xxxxxxxx: CALL +5+xxxxxxxx
			public uint operand;     // Relative destination address
		}

		// 64-bit indirect absolute call.
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct CALL_ABS {
			public byte opcode0;     // FF15 00000002: CALL [+6]
			public byte opcode1;
			public uint dummy0;
			public byte dummy1;      // EB 08:         JMP +10
			public byte dummy2;
			public ulong address;     // Absolute destination address
		}

		// 32-bit direct relative conditional jumps.
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct JCC_REL {
			public byte opcode0;     // 0F8* xxxxxxxx: J** +6+xxxxxxxx
			public byte opcode1;
			public uint operand;     // Relative destination address
		}

		// 64bit indirect absolute conditional jumps that x64 lacks.
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct JCC_ABS {
			public byte opcode;      // 7* 0E:         J** +16
			public byte dummy0;
			public byte dummy1;      // FF25 00000000: JMP [+6]
			public byte dummy2;
			public uint dummy3;
			public ulong address;     // Absolute destination address
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct TRAMPOLINE32 {
			public void* pTarget;         // [In] Address of the target function.
			public void* pDetour;         // [In] Address of the detour function.
			public void* pTrampoline;     // [In] Buffer address for the trampoline and relay function.

			public uint patchAbove;      // [Out] Should use the hot patch area?
			public uint nIP;             // [Out] Number of the instruction boundaries.
			public fixed byte oldIPs[8];       // [Out] Instruction boundaries of the target function.
			public fixed byte newIPs[8];       // [Out] Instruction boundaries of the trampoline function.
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct TRAMPOLINE64 {
			public void* pTarget;         // [In] Address of the target function.
			public void* pDetour;         // [In] Address of the detour function.
			public void* pTrampoline;     // [In] Buffer address for the trampoline and relay function.

			public void* pRelay;          // [Out] Address of the relay function.
			public uint patchAbove;      // [Out] Should use the hot patch area?
			public uint nIP;             // [Out] Number of the instruction boundaries.
			public fixed byte oldIPs[8];       // [Out] Instruction boundaries of the target function.
			public fixed byte newIPs[8];       // [Out] Instruction boundaries of the trampoline function.
		}
	}
}
