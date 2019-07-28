/*
 * Hacker Disassembler Engine 64 C
 * Copyright (c) 2008-2009, Vyacheslav Patkov.
 * All rights reserved.
 *
 */

using static MinHooking.Hde.Table64;

namespace MinHooking.Hde {
	internal static unsafe partial class Hde64 {
		public static uint hde64_disasm(void* code, hde64s* hs) {
			byte x;
			byte c = 0;
			byte* p = (byte*)code;
			byte cflags = 0;
			byte opcode;
			byte pref = 0;
			byte* ht = hde64_table;
			byte m_mod;
			byte m_reg;
			byte m_rm;
			byte disp_size = 0;
			byte op64 = 0;

			bool error_opcode_flag = false;
			bool rel32_ok_flag = false;
			bool imm16_ok_flag = false;

			for (x = 16; x != 0; x--)
				switch (c = *p++) {
				case 0xf3:
					hs->p_rep = c;
					pref |= PRE_F3;
					break;
				case 0xf2:
					hs->p_rep = c;
					pref |= PRE_F2;
					break;
				case 0xf0:
					hs->p_lock = c;
					pref |= PRE_LOCK;
					break;
				case 0x26:
				case 0x2e:
				case 0x36:
				case 0x3e:
				case 0x64:
				case 0x65:
					hs->p_seg = c;
					pref |= PRE_SEG;
					break;
				case 0x66:
					hs->p_66 = c;
					pref |= PRE_66;
					break;
				case 0x67:
					hs->p_67 = c;
					pref |= PRE_67;
					break;
				default:
					goto pref_done;
				}
			pref_done:

			hs->flags = (uint)pref << 23;

			if (pref == 0)
				pref |= PRE_NONE;

			if ((c & 0xf0) == 0x40) {
				hs->flags |= F_PREFIX_REX;
				if ((hs->rex_w = (byte)((c & 0xf) >> 3)) != 0 && (*p & 0xf8) == 0xb8)
					op64++;
				hs->rex_r = (byte)((c & 7) >> 2);
				hs->rex_x = (byte)((c & 3) >> 1);
				hs->rex_b = (byte)(c & 1);
				if (((c = *p++) & 0xf0) == 0x40) {
					opcode = c;
					error_opcode_flag = true;
					goto error_opcode;
				}
			}

			if ((hs->opcode = c) == 0x0f) {
				hs->opcode2 = c = *p++;
				ht += DELTA_OPCODES;
			}
			else if (c >= 0xa0 && c <= 0xa3) {
				op64++;
				if ((pref & PRE_67) != 0)
					pref |= PRE_66;
				else
					pref &= unchecked((byte)~PRE_66);
			}

			opcode = c;
			cflags = ht[ht[opcode / 4] + (opcode % 4)];

			error_opcode_flag = false;
		error_opcode:
			if (cflags == C_ERROR || error_opcode_flag) {
				hs->flags |= F_ERROR | F_ERROR_OPCODE;
				cflags = 0;
				if ((opcode & -3) == 0x24)
					cflags++;
			}

			x = 0;
			if ((cflags & C_GROUP) != 0) {
				ushort t;
				t = *(ushort*)(ht + (cflags & 0x7f));
				cflags = (byte)t;
				x = (byte)(t >> 8);
			}

			if (hs->opcode2 != 0) {
				ht = hde64_table + DELTA_PREFIXES;
				if ((ht[ht[opcode / 4] + (opcode % 4)] & pref) != 0)
					hs->flags |= F_ERROR | F_ERROR_OPCODE;
			}

			if ((cflags & C_MODRM) != 0) {
				hs->flags |= F_MODRM;
				hs->modrm = c = *p++;
				hs->modrm_mod = m_mod = (byte)(c >> 6);
				hs->modrm_rm = m_rm = (byte)(c & 7);
				hs->modrm_reg = m_reg = (byte)((c & 0x3f) >> 3);

				if (x != 0 && ((x << m_reg) & 0x80) != 0)
					hs->flags |= F_ERROR | F_ERROR_OPCODE;

				if (hs->opcode2 == 0 && opcode >= 0xd9 && opcode <= 0xdf) {
					byte t = (byte)(opcode - 0xd9);
					if (m_mod == 3) {
						ht = hde64_table + DELTA_FPU_MODRM + t * 8;
						t = (byte)(ht[m_reg] << m_rm);
					}
					else {
						ht = hde64_table + DELTA_FPU_REG;
						t = (byte)(ht[t] << m_reg);
					}
					if ((t & 0x80) != 0)
						hs->flags |= F_ERROR | F_ERROR_OPCODE;
				}

				if ((pref & PRE_LOCK) != 0) {
					if (m_mod == 3) {
						hs->flags |= F_ERROR | F_ERROR_LOCK;
					}
					else {
						byte* table_end;
						byte op = opcode;
						if (hs->opcode2 != 0) {
							ht = hde64_table + DELTA_OP2_LOCK_OK;
							table_end = ht + DELTA_OP_ONLY_MEM - DELTA_OP2_LOCK_OK;
						}
						else {
							ht = hde64_table + DELTA_OP_LOCK_OK;
							table_end = ht + DELTA_OP2_LOCK_OK - DELTA_OP_LOCK_OK;
							op &= unchecked((byte)-2);
						}
						for (; ht != table_end; ht++)
							if (*ht++ == op) {
								if (((*ht << m_reg) & 0x80) == 0)
									goto no_lock_error;
								else
									break;
							}
						hs->flags |= F_ERROR | F_ERROR_LOCK;
					no_lock_error:
						;
					}
				}

				if (hs->opcode2 != 0) {
					switch (opcode) {
					case 0x20:
					case 0x22:
						m_mod = 3;
						if (m_reg > 4 || m_reg == 1)
							goto error_operand;
						else
							goto no_error_operand;
					case 0x21:
					case 0x23:
						m_mod = 3;
						if (m_reg == 4 || m_reg == 5)
							goto error_operand;
						else
							goto no_error_operand;
					}
				}
				else {
					switch (opcode) {
					case 0x8c:
						if (m_reg > 5)
							goto error_operand;
						else
							goto no_error_operand;
					case 0x8e:
						if (m_reg == 1 || m_reg > 5)
							goto error_operand;
						else
							goto no_error_operand;
					}
				}

				if (m_mod == 3) {
					byte* table_end;
					if (hs->opcode2 != 0) {
						ht = hde64_table + DELTA_OP2_ONLY_MEM;
						table_end = ht + _hde64_table.Length - DELTA_OP2_ONLY_MEM;
					}
					else {
						ht = hde64_table + DELTA_OP_ONLY_MEM;
						table_end = ht + DELTA_OP2_ONLY_MEM - DELTA_OP_ONLY_MEM;
					}
					for (; ht != table_end; ht += 2)
						if (*ht++ == opcode) {
							if ((*ht++ & pref) != 0 && ((*ht << m_reg) & 0x80) == 0)
								goto error_operand;
							else
								break;
						}
					goto no_error_operand;
				}
				else if (hs->opcode2 != 0) {
					switch (opcode) {
					case 0x50:
					case 0xd7:
					case 0xf7:
						if ((pref & (PRE_NONE | PRE_66)) != 0)
							goto error_operand;
						break;
					case 0xd6:
						if ((pref & (PRE_F2 | PRE_F3)) != 0)
							goto error_operand;
						break;
					case 0xc5:
						goto error_operand;
					}
					goto no_error_operand;
				}
				else
					goto no_error_operand;

				error_operand:
				hs->flags |= F_ERROR | F_ERROR_OPERAND;
			no_error_operand:

				c = *p++;
				if (m_reg <= 1) {
					if (opcode == 0xf6)
						cflags |= C_IMM8;
					else if (opcode == 0xf7)
						cflags |= C_IMM_P66;
				}

				switch (m_mod) {
				case 0:
					if ((pref & PRE_67) != 0) {
						if (m_rm == 6)
							disp_size = 2;
					}
					else
						if (m_rm == 5)
						disp_size = 4;
					break;
				case 1:
					disp_size = 1;
					break;
				case 2:
					disp_size = 2;
					if ((pref & PRE_67) == 0)
						disp_size <<= 1;
					break;
				}

				if (m_mod != 3 && m_rm == 4) {
					hs->flags |= F_SIB;
					p++;
					hs->sib = c;
					hs->sib_scale = (byte)(c >> 6);
					hs->sib_index = (byte)((c & 0x3f) >> 3);
					if ((hs->sib_base = (byte)(c & 7)) == 5 && (m_mod & 1) == 0)
						disp_size = 4;
				}

				p--;
				switch (disp_size) {
				case 1:
					hs->flags |= F_DISP8;
					hs->disp.disp8 = *p;
					break;
				case 2:
					hs->flags |= F_DISP16;
					hs->disp.disp16 = *(ushort*)p;
					break;
				case 4:
					hs->flags |= F_DISP32;
					hs->disp.disp32 = *(uint*)p;
					break;
				}
				p += disp_size;
			}
			else if ((pref & PRE_LOCK) != 0)
				hs->flags |= F_ERROR | F_ERROR_LOCK;

			if ((cflags & C_IMM_P66) != 0) {
				if ((cflags & C_REL32) != 0) {
					if ((pref & PRE_66) != 0) {
						hs->flags |= F_IMM16 | F_RELATIVE;
						hs->imm.imm16 = *(ushort*)p;
						p += 2;
						goto disasm_done;
					}
					rel32_ok_flag = true;
					goto rel32_ok;
				}
				if (op64 != 0) {
					hs->flags |= F_IMM64;
					hs->imm.imm64 = *(ulong*)p;
					p += 8;
				}
				else if ((pref & PRE_66) == 0) {
					hs->flags |= F_IMM32;
					hs->imm.imm32 = *(uint*)p;
					p += 4;
				}
				else {
					imm16_ok_flag = true;
					goto imm16_ok;
				}
			}


			imm16_ok_flag = false;
		imm16_ok:
			if ((cflags & C_IMM16) != 0 || imm16_ok_flag) {
				hs->flags |= F_IMM16;
				hs->imm.imm16 = *(ushort*)p;
				p += 2;
			}
			if ((cflags & C_IMM8) != 0) {
				hs->flags |= F_IMM8;
				hs->imm.imm8 = *p++;
			}

			rel32_ok_flag = false;
		rel32_ok:
			if ((cflags & C_REL32) != 0 || rel32_ok_flag) {
				hs->flags |= F_IMM32 | F_RELATIVE;
				hs->imm.imm32 = *(uint*)p;
				p += 4;
			}
			else if ((cflags & C_REL8) != 0) {
				hs->flags |= F_IMM8 | F_RELATIVE;
				hs->imm.imm8 = *p++;
			}

		disasm_done:

			if ((hs->len = (byte)(p - (byte*)code)) > 15) {
				hs->flags |= F_ERROR | F_ERROR_LENGTH;
				hs->len = 15;
			}

			return hs->len;
		}
	}
}
