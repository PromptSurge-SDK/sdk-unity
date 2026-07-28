#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace PromptSurgeSDK.Editor {
    /// <summary>
    /// Writes PromptSurge's privacy manifest into the generated Xcode project.
    ///
    /// WHY THIS EXISTS. Apple aggregates one PrivacyInfo.xcprivacy per binary, and
    /// the Unity SDK's code is compiled into UnityFramework rather than shipped as
    /// its own framework, so there is nothing for a static file in the package to
    /// attach itself to. The manifest has to be added to the Xcode project at build
    /// time and given target membership, which only a post-process step can do.
    ///
    /// WHAT GOES WRONG WITHOUT IT. `UserDefaults` is a required-reason API (CA92.1)
    /// and PlayerPrefs is UserDefaults on iOS, so every customer shipping a Unity
    /// game with this SDK gets ITMS-91053 at submission, from a build that compiled
    /// perfectly and ran fine on device. Nothing in Unity warns about it.
    ///
    /// WHY THE CONTENT IS A STRING RATHER THAN AN ASSET. A `.xcprivacy` inside a UPM
    /// package is an asset Unity imports by extension, and how it is treated (and
    /// whether it survives into a build) depends on where it sits and what importer
    /// claims it. Emitting it from here means the file that reaches Xcode is exactly
    /// the file this code says it is, with no import step in between. It is kept
    /// byte-aligned with `packages/sdk-ios/Sources/PromptSurge/PrivacyInfo.xcprivacy`
    /// in intent: if one changes, change both.
    ///
    /// IF THE APP ALREADY HAS ITS OWN MANIFEST, this does not touch it. This one is
    /// added to the UnityFramework target, which is the binary PromptSurge's code
    /// actually lives in; the app's own manifest describes the app's own binary.
    ///
    /// NOT VERIFIED AGAINST A REAL UNITY BUILD. There is no Unity compiler or iOS
    /// toolchain in the environment this was written in. See
    /// docs/release-review-2026-07.md, "What still needs Unity".
    /// </summary>
    internal class PromptSurgePrivacyManifest : IPostprocessBuildWithReport {
        // After Unity's own post-processors, which create the project this edits.
        public int callbackOrder => 100;

        private const string FileName = "PrivacyInfo.xcprivacy";
        private const string Folder = "PromptSurge";

        public void OnPostprocessBuild(BuildReport report) {
            if (report.summary.platform != BuildTarget.iOS) return;

            var projectRoot = report.summary.outputPath;
            var pbxPath = PBXProject.GetPBXProjectPath(projectRoot);
            if (!File.Exists(pbxPath)) {
                Debug.LogWarning(
                    "[PromptSurge] No Xcode project at " + pbxPath +
                    "; the privacy manifest was not added. Your build will hit " +
                    "ITMS-91053 at submission unless you add it by hand.");
                return;
            }

            var relativeDir = Path.Combine(Folder, FileName).Replace("\\", "/");
            var absolutePath = Path.Combine(projectRoot, relativeDir);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, ManifestXml);

            var project = new PBXProject();
            project.ReadFromFile(pbxPath);

            // UnityFramework, not the app target: that is the binary PromptSurge's
            // code is linked into, and a manifest describes the binary it ships in.
            var targetGuid = project.GetUnityFrameworkTargetGuid();
            var fileGuid = project.AddFile(relativeDir, relativeDir, PBXSourceTree.Source);
            project.AddFileToBuild(targetGuid, fileGuid);
            project.WriteToFile(pbxPath);

            Debug.Log("[PromptSurge] Added " + relativeDir + " to UnityFramework.");
        }

        // Kept in step with the iOS SDK's manifest. Every entry is derived from what
        // Telemetry.cs actually sends: a hashed per-install device id and the six
        // event types, both for analytics, neither linked to identity, no IDFA and
        // no ATT prompt anywhere in the SDK.
        private const string ManifestXml =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
  <key>NSPrivacyTracking</key>
  <false/>
  <key>NSPrivacyTrackingDomains</key>
  <array/>
  <key>NSPrivacyCollectedDataTypes</key>
  <array>
    <dict>
      <key>NSPrivacyCollectedDataType</key>
      <string>NSPrivacyCollectedDataTypeDeviceID</string>
      <key>NSPrivacyCollectedDataTypeLinked</key>
      <false/>
      <key>NSPrivacyCollectedDataTypeTracking</key>
      <false/>
      <key>NSPrivacyCollectedDataTypePurposes</key>
      <array>
        <string>NSPrivacyCollectedDataTypePurposeAnalytics</string>
      </array>
    </dict>
    <dict>
      <key>NSPrivacyCollectedDataType</key>
      <string>NSPrivacyCollectedDataTypeProductInteraction</string>
      <key>NSPrivacyCollectedDataTypeLinked</key>
      <false/>
      <key>NSPrivacyCollectedDataTypeTracking</key>
      <false/>
      <key>NSPrivacyCollectedDataTypePurposes</key>
      <array>
        <string>NSPrivacyCollectedDataTypePurposeAnalytics</string>
      </array>
    </dict>
  </array>
  <key>NSPrivacyAccessedAPITypes</key>
  <array>
    <dict>
      <key>NSPrivacyAccessedAPIType</key>
      <string>NSPrivacyAccessedAPICategoryUserDefaults</string>
      <key>NSPrivacyAccessedAPITypeReasons</key>
      <array>
        <string>CA92.1</string>
      </array>
    </dict>
  </array>
</dict>
</plist>
";
    }
}
#endif
