using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PluginCore;
using ProjectManager.Actions;
using ProjectManager.Projects.AS3;

namespace ExportSWC.Utils
{
    public static class ApacheFlexSdkCompatibility
    {
        public static void SetApacheFlexCompatibilityEnvironment(this IDictionary<string, string> env, AS3Project project)
        {
            if (project.Language == "as3")
            {
                var playerglobalHome = Environment.ExpandEnvironmentVariables("%PLAYERGLOBAL_HOME%");
                if (playerglobalHome.StartsWith('%'))
                {
                    env["PLAYERGLOBAL_HOME"] = Path.Combine(project.CurrentSDK ?? project.PreferredSDK, "frameworks/libs/player");
                }

                var airHome = Environment.ExpandEnvironmentVariables("%AIR_HOME%");
                if (airHome.StartsWith('%'))
                {
                    var airSdk = project.CurrentSDK ?? project.PreferredSDK;

                    if (!ContainsAirConfig(airSdk))
                    {
                        var sdks = BuildActions.GetInstalledSDKs(project);
                        foreach (var sdk in sdks)
                        {
                            if (ContainsAirConfig(sdk.Path))
                            {
                                airHome = sdk.Path;
                                break;
                            }
                        }
                    }

                    //if (airSdk.IndexOf("AIR", StringComparison.OrdinalIgnoreCase) == -1)
                    //{
                    //    var sdks = BuildActions.GetInstalledSDKs(project);
                    //    airSdk = sdks
                    //        .Select(x => new
                    //        {
                    //            Index = x.Name.IndexOf("AIR", StringComparison.OrdinalIgnoreCase),
                    //            Sdk = x
                    //        })
                    //        .OrderBy(x => x.Index == -1 ? 10000 : x.Index)
                    //        .FirstOrDefault()
                    //        .Sdk.Path;
                    //}

                    if (airSdk is not null)
                    {
                        // Hack
                        env["AIR_HOME"] = airSdk;
                    }
                }

                var targetPlayerMajorVersion = Environment.ExpandEnvironmentVariables("%TARGETPLAYER_MAJOR_VERSION%");
                if (targetPlayerMajorVersion.StartsWith('%'))
                {
                    env["TARGETPLAYER_MAJOR_VERSION"] = project.MovieOptions.Version.Substring(0, project.MovieOptions.Version.IndexOf('.'));
                }
                var targetPlayerMinorVersion = Environment.ExpandEnvironmentVariables("%TARGETPLAYER_MINOR_VERSION%");
                if (targetPlayerMinorVersion.StartsWith('%'))
                {
                    var majorDot = project.MovieOptions.Version.IndexOf('.');
                    env["TARGETPLAYER_MINOR_VERSION"] = project.MovieOptions.Version.Substring(majorDot + 1);
                }

                var locale = Environment.ExpandEnvironmentVariables("%LOCALE%");
                if (locale.StartsWith('%') &&
                    !string.IsNullOrEmpty(project.CompilerOptions.Locale))
                {
                    env["LOCALE"] = project.CompilerOptions.Locale;
                }
            }
        }

        private static bool ContainsAirConfig(string sdkPath)
        {
            return File.Exists(Path.Combine(sdkPath, "air-sdk-description.xml"));
        }
    }
}
