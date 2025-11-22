#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

/// <summary>
/// iOS 빌드 후 Info.plist를 자동으로 수정하여 HTTP 통신을 허용합니다.
/// </summary>
public class iOSBuildPostProcessor
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget == BuildTarget.iOS)
        {
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            PlistElementDict rootDict = plist.root;

            // App Transport Security 설정 추가
            PlistElementDict atsDict = rootDict.CreateDict("NSAppTransportSecurity");
            
            // 모든 HTTP 통신 허용 (개발/테스트용)
            // 프로덕션에서는 특정 도메인만 허용하는 것이 좋습니다.
            atsDict.SetBoolean("NSAllowsArbitraryLoads", true);

            // 특정 도메인만 허용하려면 아래 코드를 사용하세요:
            /*
            PlistElementDict exceptionDomainsDict = atsDict.CreateDict("NSExceptionDomains");
            PlistElementDict domainDict = exceptionDomainsDict.CreateDict("hojoon.ddns.net");
            domainDict.SetBoolean("NSExceptionAllowsInsecureHTTPLoads", true);
            domainDict.SetBoolean("NSIncludesSubdomains", true);
            */

            plist.WriteToFile(plistPath);
            UnityEngine.Debug.Log("✅ iOS Info.plist에 HTTP 통신 허용 설정이 추가되었습니다.");
        }
    }
}
#endif
