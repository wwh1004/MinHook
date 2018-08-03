/*
 * Hacker Disassembler Engine 32
 * Copyright (c) 2006-2009, Vyacheslav Patkov.
 * All rights reserved.
 *
 * hde32.h: C/C++ header file
 *
 */

using System.Runtime.InteropServices;

namespace MinHooks.Hde {
	internal static unsafe partial class Hde32 {
		public const uint F_MODRM = 0x00000001;
		public const uint F_SIB = 0x00000002;
		public const uint F_IMM8 = 0x00000004;
		public const uint F_IMM16 = 0x00000008;
		public const uint F_IMM32 = 0x00000010;
		public const uint F_DISP8 = 0x00000020;
		public const uint F_DISP16 = 0x00000040;
		public const uint F_DISP32 = 0x00000080;
		public const uint F_RELATIVE = 0x00000100;
		public const uint F_2IMM16 = 0x00000800;
		public const uint F_ERROR = 0x00001000;
		public const uint F_ERROR_OPCODE = 0x00002000;
		public const uint F_ERROR_LENGTH = 0x00004000;
		public const uint F_ERROR_LOCK = 0x00008000;
		public const uint F_ERROR_OPERAND = 0x00010000;
		public const uint F_PREFIX_REPNZ = 0x01000000;
		public const uint F_PREFIX_REPX = 0x02000000;
		public const uint F_PREFIX_REP = 0x03000000;
		public const uint F_PREFIX_66 = 0x04000000;
		public const uint F_PREFIX_67 = 0x08000000;
		public const uint F_PREFIX_LOCK = 0x10000000;
		public const uint F_PREFIX_SEG = 0x20000000;
		public const uint F_PREFIX_ANY = 0x3f000000;

		public const byte PREFIX_SEGMENT_CS = 0x2e;
		public const byte PREFIX_SEGMENT_SS = 0x36;
		public const byte PREFIX_SEGMENT_DS = 0x3e;
		public const byte PREFIX_SEGMENT_ES = 0x26;
		public const byte PREFIX_SEGMENT_FS = 0x64;
		public const byte PREFIX_SEGMENT_GS = 0x65;
		public const byte PREFIX_LOCK = 0xf0;
		public const byte PREFIX_REPNZ = 0xf2;
		public const byte PREFIX_REPX = 0xf3;
		public const byte PREFIX_OPERAND_SIZE = 0x66;
		public const byte PREFIX_ADDRESS_SIZE = 0x67;

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct hde32s {
			public byte len;
			public byte p_rep;
			public byte p_lock;
			public byte p_seg;
			public byte p_66;
			public byte p_67;
			public byte opcode;
			public byte opcode2;
			public byte modrm;
			public byte modrm_mod;
			public byte modrm_reg;
			public byte modrm_rm;
			public byte sib;
			public byte sib_scale;
			public byte sib_index;
			public byte sib_base;
			public imm imm;
			public disp disp;
			public uint flags;
		}

		[StructLayout(LayoutKind.Explicit, Pack = 1)]
		public struct imm {
			[FieldOffset(0)]
			public byte imm8;
			[FieldOffset(0)]
			public ushort imm16;
			[FieldOffset(0)]
			public uint imm32;
		}

		[StructLayout(LayoutKind.Explicit, Pack = 1)]
		public struct disp {
			[FieldOffset(0)]
			public byte disp8;
			[FieldOffset(0)]
			public ushort disp16;
			[FieldOffset(0)]
			public uint disp32;
		}
	}
}
