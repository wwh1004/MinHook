using System;
using System.Runtime.InteropServices;

namespace MinHooks {
	internal static unsafe class NativeMethods {
		public const byte FALSE = 0;

		public const byte TRUE = 1;

		public static bool WIN64 = IntPtr.Size == 8;

		[StructLayout(LayoutKind.Sequential)]
		public struct MEMORY_BASIC_INFORMATION {
			public byte* BaseAddress;
			public byte* AllocationBase;
			public uint AllocationProtect;
			public byte* RegionSize;
			public uint State;
			public uint Protect;
			public uint Type;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SYSTEM_INFO {
			public uint dwOemId;
			public uint dwPageSize;
			public byte* lpMinimumApplicationAddress;
			public byte* lpMaximumApplicationAddress;
			public uint dwActiveProcessorMask;
			public uint dwNumberOfProcessors;
			public uint dwProcessorType;
			public uint dwAllocationGranularity;
			public ushort wProcessorLevel;
			public ushort wProcessorRevision;
		}

		[StructLayout(LayoutKind.Explicit, Size = 0x2cc)]
		public struct CONTEXT32 {
			[FieldOffset(0)]
			public uint ContextFlags;
			[FieldOffset(0xb8)]
			public uint Eip;
		}

		[StructLayout(LayoutKind.Explicit, Size = 0x4d0)]
		public struct CONTEXT64 {
			[FieldOffset(0x30)]
			public uint ContextFlags;
			[FieldOffset(0xf8)]
			public ulong Rip;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct THREADENTRY32 {
			public uint dwSize;
			public uint cntUsage;
			public uint th32ThreadID;
			public uint th32OwnerProcessID;
			public int tpBasePri;
			public int tpDeltaPri;
			public uint dwFlags;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern void* VirtualAlloc(void* lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint VirtualFree(void* lpAddress, uint dwSize, uint dwFreeType);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint VirtualProtect(void* lpAddress, uint dwSize, uint flNewProtect, uint* lpflOldProtect);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern void* VirtualQuery(void* lpAddress, MEMORY_BASIC_INFORMATION* lpBuffer, uint dwLength);

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern void GetSystemInfo(SYSTEM_INFO* lpSystemInfo);

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern uint GetThreadContext(void* hThread, void* lpContext);

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern uint SetThreadContext(void* hThread, void* lpContext);

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern void* HeapCreate(uint flOptions, uint dwInitialSize, uint dwMaximumSize);

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern void* HeapAlloc(void* hHeap, uint dwFlags, uint dwBytes);

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern void* HeapReAlloc(void* hHeap, uint dwFlags, void* lpMem, uint dwBytes);

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern uint HeapFree(void* hHeap, uint dwFlags, void* lpMem);

		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern uint HeapDestroy(void* hHeap);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern void* OpenThread(uint dwDesiredAccess, uint bInheritHandle, uint dwThreadId);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint ResumeThread(void* hThread);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint SuspendThread(void* hThread);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint CloseHandle(void* hObject);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint GetCurrentProcessId();

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint GetCurrentThreadId();

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern void* GetCurrentProcess();

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint FlushInstructionCache(void* hProcess, void* lpBaseAddress, uint dwSize);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern void* CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint Thread32First(void* hSnapshot, THREADENTRY32* lpte);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern uint Thread32Next(void* hSnapshot, THREADENTRY32* lpte);

		[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW", ExactSpelling = true, SetLastError = true)]
		public static extern void* GetModuleHandle(string lpModuleName);

		[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Ansi, EntryPoint = "GetProcAddress", ExactSpelling = true, SetLastError = true)]
		public static extern void* GetProcAddress(void* hModule, string lpProcName);

		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
		public static extern void* memcpy(void* _Dst, void* _Src, uint _Size);
	}
}
