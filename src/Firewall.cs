using System;

namespace SteamOfflineSwitch
{
    // 只用 Windows 防火墙按“程序路径”拦截 steam.exe 的出站流量。
    // 不触碰 SteamService.exe，因此 Steam 客户端本身仍可正常登录/更新。
    // 规则默认 Disabled：切换时只启/停，无需重启 Steam。
    internal static class Firewall
    {
        private const int DirectionOut = 2;   // NET_FW_RULE_DIR_OUT
        private const int ActionBlock = 0;    // NET_FW_ACTION_BLOCK
        private const int ProtocolAny = 256;  // NET_FW_IP_PROTOCOL_ANY
        private const int ProfilesAll = 0x7FFFFFFF;

        private static string[] Names()
        {
            string[] a = new string[AppInfo.RuleCount];
            for (int i = 0; i < a.Length; i++) a[i] = AppInfo.RulePrefix + "_" + i;
            return a;
        }

        private static dynamic NewPolicy()
        {
            Type t = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (t == null) throw new InvalidOperationException("无法加载 Windows 防火墙组件 (HNetCfg.FwPolicy2)。");
            return Activator.CreateInstance(t);
        }

        // 任意一条规则处于启用状态即视为“离线中”。
        public static bool IsBlocked()
        {
            try
            {
                dynamic policy = NewPolicy();
                dynamic rules = policy.Rules;
                bool found = false, anyEnabled = false;
                string[] names = Names();
                for (int i = 0; i < names.Length; i++)
                {
                    try
                    {
                        dynamic r = rules.Item(names[i]);
                        found = true;
                        if ((bool)r.Enabled) anyEnabled = true;
                    }
                    catch { }
                }
                return found && anyEnabled;
            }
            catch (Exception ex) { Log.E("读取防火墙状态失败", ex); return false; }
        }

        public static bool RulesExist()
        {
            try
            {
                dynamic policy = NewPolicy();
                dynamic rules = policy.Rules;
                string[] names = Names();
                for (int i = 0; i < names.Length; i++)
                {
                    try { dynamic r = rules.Item(names[i]); }
                    catch { return false; }
                }
                return true;
            }
            catch { return false; }
        }

        // 返回第一条规则所绑定的程序路径（用于检测 Steam 路径是否被改动）。
        public static string GetRuleApplication()
        {
            try
            {
                dynamic policy = NewPolicy();
                dynamic rules = policy.Rules;
                dynamic r = rules.Item(Names()[0]);
                return (string)r.ApplicationName;
            }
            catch { return null; }
        }

        // 删除同名旧规则并重建（全部为 Disabled）。
        public static bool EnsureRules(string steamExe)
        {
            try
            {
                dynamic policy = NewPolicy();
                dynamic rules = policy.Rules;
                string[] names = Names();
                for (int i = 0; i < names.Length; i++)
                {
                    try { rules.Remove(names[i]); } catch { }
                }
                for (int i = 0; i < names.Length; i++)
                {
                    dynamic r = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwRule"));
                    r.Name = names[i];
                    r.Description = AppInfo.Title + " —— 阻断指定 Steam 程序的联网（用于家庭共享离线游玩）";
                    r.Direction = DirectionOut;
                    r.Action = ActionBlock;
                    r.Protocol = ProtocolAny;
                    r.Profiles = ProfilesAll;
                    r.ApplicationName = steamExe;
                    r.Enabled = false;
                    rules.Add(r);
                }
                Log.I("已创建防火墙规则 x" + names.Length + " -> " + steamExe);
                return true;
            }
            catch (Exception ex) { Log.E("创建防火墙规则失败", ex); return false; }
        }

        public static bool SetBlocked(bool block)
        {
            try
            {
                dynamic policy = NewPolicy();
                dynamic rules = policy.Rules;
                string[] names = Names();
                for (int i = 0; i < names.Length; i++)
                {
                    dynamic r = rules.Item(names[i]);
                    r.Enabled = block;
                }
                Log.I("切换联网状态 -> " + (block ? "阻断(离线)" : "放行(在线)"));
                return true;
            }
            catch (Exception ex) { Log.E("切换防火墙规则失败", ex); return false; }
        }

        // 删除本工具创建的全部规则（用于卸载 / 彻底恢复）。
        public static bool RemoveAll()
        {
            try
            {
                dynamic policy = NewPolicy();
                dynamic rules = policy.Rules;
                string[] names = Names();
                for (int i = 0; i < names.Length; i++)
                {
                    try { rules.Remove(names[i]); } catch { }
                }
                Log.I("已删除全部防火墙规则");
                return true;
            }
            catch (Exception ex) { Log.E("删除防火墙规则失败", ex); return false; }
        }
    }
}
