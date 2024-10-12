using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace LCMyMango
{
    public class MangoConfigPrimitive
    {
        public float TimeUntilExplode;
        public float ExplodeCooldown;
	}

	public class MangoConfigPacket
	{
		public enum PacketType
		{
			Request,
			Receive
		}

		public MangoConfigPrimitive? Config;
		public PacketType Type;
	}

	public class MangoConfig
	{
        private readonly ConfigEntry<float> _voiceThreshold;
        private readonly ConfigEntry<float> _timeUntilExplode;
        private readonly ConfigEntry<float> _explodeCooldown;

        public float VoiceThreshold => _voiceThreshold.Value;
		public float TimeUntilExplode => _timeUntilExplode.Value;
		public float ExplodeCooldown => _explodeCooldown.Value;

		public MangoConfig(ConfigFile cfg)
        {
            cfg.SaveOnConfigSet = false;

            _voiceThreshold = cfg.Bind(
                "Voice",
                "VoiceThreshold",
                0.2f,
                "The minimum voice loudness required to start considering whether to spawn a mine. Is not synced with the host."
            );

            _timeUntilExplode = cfg.Bind(
                "Host",
                "TimeUntilExplode",
                0.5f,
                "The minimum time required screaming to spawn a mine. Is synced with the host."
            );

            _explodeCooldown = cfg.Bind(
                "Host",
                "ExplodeCooldown",
                2f,
                "Minimum required time to wait until another mine can be spawned again. Is synced with the host."
            );

            ClearOrphanedEntries(cfg);
            cfg.Save();
            cfg.SaveOnConfigSet = true;
        }

        static void ClearOrphanedEntries(ConfigFile cfg)
        {
            // Find the private property `OrphanedEntries` from the type `ConfigFile`
            PropertyInfo orphanedEntriesProp = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");
            // And get the value of that property from our ConfigFile instance
            var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg);
            // And finally, clear the `OrphanedEntries` dictionary
            orphanedEntries.Clear();
        }
    }
}
