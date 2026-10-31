using System;

namespace ZzzSwitch
{
    // 渠道真值表：唯一真相来源，代码与 README 共用。
    // 数值来自两台真实安装的 config.ini 实测（官服 cps=zzz_gw_pc，不是旧 README 里的 zzz_mktbackup2_pc）。
    public sealed class ChannelProfile
    {
        public string Key;
        public string Display;
        public string Channel;
        public string SubChannel;
        public string Cps;
        public string SdkDirRel;        // null => 该渠道不额外要求渠道 SDK
        public string SdkPluginKey;     // null => config.ini 中不应存在该键
        public string LauncherSchema;   // HKCU\Software\miHoYo\HYP\<schema>\GameInstallPath

        public bool RequiresSdk { get { return !string.IsNullOrEmpty(SdkDirRel); } }

        public static readonly ChannelProfile Official = new ChannelProfile
        {
            Key = "official",
            Display = "官服(米哈游)",
            Channel = "1",
            SubChannel = "1",
            Cps = "zzz_gw_pc",
            SdkDirRel = null,
            SdkPluginKey = null,
            LauncherSchema = @"1_1\nap_cn"
        };

        public static readonly ChannelProfile Bilibili = new ChannelProfile
        {
            Key = "bilibili",
            Display = "B服(哔哩哔哩)",
            Channel = "14",
            SubChannel = "0",
            Cps = "zzz_bilibili_pc",
            SdkDirRel = @"ZenlessZoneZero_Data\Plugins\x86_64\BLPlatform64",
            SdkPluginKey = "plugin_sdk_version",
            LauncherSchema = @"standalone\14_0\nap_cn\xV0f4r1GT0\nap_cn"
        };

        public static ChannelProfile[] All()
        {
            return new ChannelProfile[] { Official, Bilibili };
        }

        public static ChannelProfile FromKey(string key)
        {
            if (key == null) return null;
            string k = key.Trim().ToLowerInvariant();
            if (k.Length == 0) return null;
            for (int i = 0; i < All().Length; i++)
                if (All()[i].Key == k) return All()[i];
            if (k == "b" || k == "bili" || k == "14" || k == "b服" || k == "哔哩哔哩") return Bilibili;
            if (k == "g" || k == "gw" || k == "1" || k == "官服" || k == "米哈游" || k == "official-client") return Official;
            return null;
        }

        public static ChannelProfile FromChannel(int channel)
        {
            for (int i = 0; i < All().Length; i++)
                if (All()[i].Channel == channel.ToString()) return All()[i];
            return null;
        }

        public static string NameOf(int channel)
        {
            ChannelProfile p = FromChannel(channel);
            return p == null ? ("未知(channel=" + channel + ")") : p.Display;
        }
    }
}
