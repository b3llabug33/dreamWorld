using System;
using UnityEngine;

namespace DreamWorld.Net
{
    public enum NetMode { Solo, Host, Join, Server }

    // How the next world session should connect. The creator screen sets it (saved in
    // PlayerPrefs); command-line flags override it, which is how servers and test clients start:
    //   dreamWorld.exe -server            dedicated server, no player of its own
    //   dreamWorld.exe -host              play and let others join you
    //   dreamWorld.exe -join 100.64.0.5   join a server/host at that address
    public static class NetSession
    {
        public const ushort Port = 7770;
        const string ModeKey = "dreamWorld.net.mode";
        const string AddressKey = "dreamWorld.net.address";

        public static NetMode Mode
        {
            get => (NetMode)PlayerPrefs.GetInt(ModeKey, 0);
            set { PlayerPrefs.SetInt(ModeKey, (int)value); PlayerPrefs.Save(); }
        }

        public static string Address
        {
            get => PlayerPrefs.GetString(AddressKey, "127.0.0.1");
            set { PlayerPrefs.SetString(AddressKey, value); PlayerPrefs.Save(); }
        }

        // True when the game was started with -server / -host / -join.
        public static bool FromCommandLine(out NetMode mode, out string address)
        {
            mode = NetMode.Solo;
            address = Address;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-server": mode = NetMode.Server; return true;
                    case "-host": mode = NetMode.Host; return true;
                    case "-join":
                        mode = NetMode.Join;
                        if (i + 1 < args.Length) address = args[i + 1];
                        return true;
                }
            }
            return false;
        }

        // The mode to use right now: command line wins, then what the creator screen saved.
        public static NetMode Resolve(out string address)
        {
            if (FromCommandLine(out var mode, out address)) return mode;
            address = Address;
            return Mode;
        }
    }
}
