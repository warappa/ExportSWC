using System;
using System.Collections.Generic;
using System.IO;
using ProjectManager.Actions;
using ProjectManager.Projects.AS3;

namespace ExportSWC.Utils
{
    internal static class ApacheFlexSdkCompatibility
    {
        public static void SetApacheFlexCompatibilityEnvironment(this IDictionary<string, string> env, AS3Project project)
        {
            if (project.Language == "as3")
            {
                // force english diagnostics location (file, line, column)
                env["JAVA_TOOL_OPTIONS"] = "-Duser.language=en-US";

                // see <sdk>\build.xml target "fix-config-file-for-flashbuilder":
                // !wrong information!: "{playerglobalHome} replaced with libs/player/{targetPlayerMajorVersion}.{targetPlayerMinorVersion}"
                // !actual information!: "{playerglobalHome} replaced with libs/player"
                var playerglobalHome = Environment.ExpandEnvironmentVariables("%PLAYERGLOBAL_HOME%");
                if (playerglobalHome.StartsWith('%'))
                {
                    //env["PLAYERGLOBAL_HOME"] = Path.Combine(project.CurrentSDK, "frameworks/libs/player", $"{project.MovieOptions.MajorVersion}.{project.MovieOptions.MinorVersion}");
                    env["PLAYERGLOBAL_HOME"] = Path.Combine(project.CurrentSDK, "frameworks/libs/player");
                }
                
                // see <sdk>\build.xml target "fix-config-file-for-flashbuilder":
                // "{airHome}/frameworks/ is removed so path left as libs/air"
                // so "airHome" is just the root of the SDK
                // We don't replace but provide environment variables to make it also work
                var airHome = Environment.ExpandEnvironmentVariables("%AIR_HOME%");
                if (airHome.StartsWith('%'))
                {
                    var airSdk = project.CurrentSDK;

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
