using System.Runtime.InteropServices;

namespace EmulatorPCHub.Controllers.Sdl;

/// <summary>Minimale P/Invoke-Anbindung an SDL3 (nur Gamepad-/Joystick-Teil).</summary>
internal static unsafe partial class Sdl3
{
    private const string Lib = "SDL3";

    public const uint InitGamepad = 0x00002000;

    public const uint EventGamepadAxisMotion = 0x650;
    public const uint EventGamepadButtonDown = 0x651;
    public const uint EventGamepadButtonUp = 0x652;
    public const uint EventGamepadAdded = 0x653;
    public const uint EventGamepadRemoved = 0x654;
    public const uint EventJoystickBatteryUpdated = 0x607;

    public enum Button
    {
        South = 0, East, West, North, Back, Guide, Start, LeftStick, RightStick,
        LeftShoulder, RightShoulder, DpadUp, DpadDown, DpadLeft, DpadRight,
    }

    public enum Axis { LeftX = 0, LeftY, RightX, RightY, LeftTrigger, RightTrigger }

    public enum ButtonLabel { Unknown = 0, A, B, X, Y, Cross, Circle, Square, Triangle }

    public enum GamepadType
    {
        Unknown = 0, Standard, Xbox360, XboxOne, PS3, PS4, PS5, SwitchPro, JoyConLeft, JoyConRight, JoyConPair,
    }

    public enum PowerState { Error = -1, Unknown = 0, OnBattery, NoBattery, Charging, Charged }

    public enum ConnectionState { Invalid = -1, Unknown = 0, Wired, Wireless }

    [StructLayout(LayoutKind.Sequential)]
    public struct Guid
    {
        public fixed byte Data[16];
    }

    /// <summary>SDL_Event ist eine 128-Byte-Union; nur der Typ und <c>which</c> werden gelesen.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct Event
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(16)] public uint Which;
    }

    [LibraryImport(Lib, EntryPoint = "SDL_Init")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool Init(uint flags);

    [LibraryImport(Lib, EntryPoint = "SDL_Quit")]
    public static partial void Quit();

    [LibraryImport(Lib, EntryPoint = "SDL_SetHint", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetHint(string name, string value);

    [LibraryImport(Lib, EntryPoint = "SDL_GetError")]
    public static partial IntPtr GetErrorPtr();

    public static string GetError() => Marshal.PtrToStringUTF8(GetErrorPtr()) ?? "";

    [LibraryImport(Lib, EntryPoint = "SDL_PollEvent")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool PollEvent(Event* ev);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepads")]
    public static partial uint* GetGamepads(int* count);

    [LibraryImport(Lib, EntryPoint = "SDL_free")]
    public static partial void Free(void* mem);

    [LibraryImport(Lib, EntryPoint = "SDL_OpenGamepad")]
    public static partial IntPtr OpenGamepad(uint instanceId);

    [LibraryImport(Lib, EntryPoint = "SDL_CloseGamepad")]
    public static partial void CloseGamepad(IntPtr gamepad);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadName")]
    public static partial IntPtr GetGamepadNamePtr(IntPtr gamepad);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadSerial")]
    public static partial IntPtr GetGamepadSerialPtr(IntPtr gamepad);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadType")]
    public static partial GamepadType GetGamepadType(IntPtr gamepad);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadVendor")]
    public static partial ushort GetGamepadVendor(IntPtr gamepad);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadProduct")]
    public static partial ushort GetGamepadProduct(IntPtr gamepad);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadGUIDForID")]
    public static partial Guid GetGamepadGuidForId(uint instanceId);

    [LibraryImport(Lib, EntryPoint = "SDL_GUIDToString")]
    public static partial void GuidToString(Guid guid, byte* buffer, int size);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadButton")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool GetGamepadButton(IntPtr gamepad, Button button);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadAxis")]
    public static partial short GetGamepadAxis(IntPtr gamepad, Axis axis);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadButtonLabel")]
    public static partial ButtonLabel GetGamepadButtonLabel(IntPtr gamepad, Button button);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadPowerInfo")]
    public static partial PowerState GetGamepadPowerInfo(IntPtr gamepad, int* percent);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadConnectionState")]
    public static partial ConnectionState GetGamepadConnectionState(IntPtr gamepad);

    [LibraryImport(Lib, EntryPoint = "SDL_SetGamepadPlayerIndex")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetGamepadPlayerIndex(IntPtr gamepad, int playerIndex);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadPlayerIndex")]
    public static partial int GetGamepadPlayerIndex(IntPtr gamepad);

    [LibraryImport(Lib, EntryPoint = "SDL_RumbleGamepad")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool RumbleGamepad(IntPtr gamepad, ushort low, ushort high, uint durationMs);

    [LibraryImport(Lib, EntryPoint = "SDL_IsJoystickVirtual")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool IsJoystickVirtual(uint instanceId);

    [LibraryImport(Lib, EntryPoint = "SDL_GetGamepadMapping")]
    public static partial IntPtr GetGamepadMappingPtr(IntPtr gamepad);

    /// <summary>SDL-Mapping-String des Geräts („guid,name,a:b0,b:b1,…“) – zeigt, welcher Rohknopf welcher Gamepad-Taste entspricht.</summary>
    public static string? GetGamepadMapping(IntPtr gamepad)
    {
        var ptr = GetGamepadMappingPtr(gamepad);
        if (ptr == IntPtr.Zero)
            return null;
        try { return Marshal.PtrToStringUTF8(ptr); }
        finally { Free((void*)ptr); }
    }

    public static string GuidString(uint instanceId)
    {
        var guid = GetGamepadGuidForId(instanceId);
        var buffer = stackalloc byte[64];
        GuidToString(guid, buffer, 64);
        return Marshal.PtrToStringUTF8((IntPtr)buffer) ?? "";
    }
}
