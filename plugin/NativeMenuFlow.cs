internal enum NativeHomeControl
{
    Unsupported,
    Traditional,
    Collection,
    OpenPacks,
    Store,
    DungeonRun
}

internal enum NativeMenuTarget
{
    None,
    Picker,
    Collection,
    Pack,
    // The local shop already opened the store (NativeShop fires the STORE press itself): nothing to route or block.
    LocalShop,
    // The Dungeon Run screen, a dialog over the HUB (NativeDungeonRunUI).
    DungeonRun
}

internal static class NativeMenuFlow
{
    internal static NativeMenuTarget HomeRoute(NativeHomeControl control)
    {
        if (control == NativeHomeControl.Traditional) return NativeMenuTarget.Picker;
        if (control == NativeHomeControl.Collection) return NativeMenuTarget.Collection;
        if (control == NativeHomeControl.OpenPacks) return NativeMenuTarget.Pack;
        if (control == NativeHomeControl.Store) return NativeMenuTarget.LocalShop;
        if (control == NativeHomeControl.DungeonRun) return NativeMenuTarget.DungeonRun;
        return NativeMenuTarget.None;
    }

    internal static bool CollectionExitAllowed(bool loggedIn, bool localActive, bool pickerOrigin) =>
        loggedIn || (localActive && pickerOrigin);

    internal static bool FatalPackDisconnectReturnsHome(bool replayActive) => !replayActive;
}
