using UnityEngine;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEditor.Callbacks;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System;

public class build : MonoBehaviour {
	static string lastPlatformPath = "/Volumes/Jesper Ext/unity/ZeGame/ZeGame/lastPlatform.txt";
	enum platform {ios, android, osx, windows, windows64, linux, web}
	static platform lastPlatform{
		get{
			// PlayerPrefs doesn't work because of inconsistency between platforms
			string text = System.IO.File.ReadAllText(lastPlatformPath);
			int platformId = Convert.ToInt32(text);
			return (platform)platformId;
		}
		set{
			int platformId = (int)value;
			string text = platformId.ToString();
			System.IO.File.WriteAllText(lastPlatformPath, text);
			// PlayerPrefs.SetInt("buildPlatform",(int)value);
		}
	}
	static string[] levels{
		get{
			if(lastPlatform == platform.web){
				return new string[]{
					"Assets/scenes/levelPlayer.unity",
					"Assets/scenes/donate.unity"
				};
			}
			
			List<string> mainLevels = new List<string>{
				"Assets/scenes/splash.unity",
				"Assets/scenes/levelSelect.unity",
				"Assets/scenes/levelPlayer.unity",
				"Assets/scenes/levelEditor.unity",
				"Assets/scenes/customLevelSelect.unity",
				"Assets/scenes/donate.unity",
				"Assets/scenes/settings.unity",
				"Assets/scenes/credits.unity"
			};

			if(lastPlatform != platform.ios && lastPlatform != platform.osx){
				mainLevels.Add("Assets/scenes/achievements.unity");
			}

			return mainLevels.ToArray();
		}
	}

	static BuildTarget bt{
		get{
			switch(lastPlatform){
				case platform.ios:
					return BuildTarget.iOS;
				case platform.android:
					return BuildTarget.Android;
				case platform.osx:
					return BuildTarget.StandaloneOSXUniversal;
				case platform.windows:
					return BuildTarget.StandaloneWindows;
				case platform.windows64:
					return BuildTarget.StandaloneWindows64;
				case platform.linux:
					return BuildTarget.StandaloneLinuxUniversal;
				case platform.web:
					return BuildTarget.WebGL;
				default:
					return BuildTarget.iOS;
			}
		}
	}

	static string buildPath{
		get{
			switch(lastPlatform){
				case platform.ios:
					return "/Volumes/Jesper Ext/unity/ZeGame/ZeGame_ios/xcode";
				case platform.android:
					return "/Volumes/Jesper Ext/unity/ZeGame/ZeGame_android.apk";
				case platform.osx:
					return "/Volumes/Jesper Ext/unity/ZeGame/ZeGame_osx/ZeGame";
				case platform.windows:
					return "/Volumes/Jesper Ext/unity/ZeGame/ZeGame_windows/32/ZeGame.exe";
				case platform.windows64:
					return "/Volumes/Jesper Ext/unity/ZeGame/ZeGame_windows/64/ZeGame.exe";
				case platform.linux:
					return "/Volumes/Jesper Ext/unity/ZeGame/ZeGame_linux/payload/ZeGame";
				case platform.web:
					return "/Volumes/Jesper Ext/unity/ZeGame/ZeGame_webGL";
				default:
					return "../builds/default";
			}
		}
	}

	static BuildOptions bo{
		get{
			switch(lastPlatform){
				case platform.ios:
				case platform.android:
				case platform.web:
					return BuildOptions.AutoRunPlayer;
				default:
					return BuildOptions.None;
			}
		}
	}

	[MenuItem ("Build/current %e")]
	static void buildLast(){
		PlayerSettings.productName = lastPlatform == platform.osx ? "ZeGame.osx" : "ZeGame";
		PlayerSettings.displayResolutionDialog = lastPlatform == platform.linux ? ResolutionDialogSetting.Enabled : ResolutionDialogSetting.HiddenByDefault;
		if(lastPlatform == platform.web){
			moveResources("levels");
		}else{
			moveResources("webLevels");
		}
		if(lastPlatform == platform.osx || lastPlatform == platform.ios){
			moveResources("achievementIcons");
		}
		BuildPipeline.BuildPlayer(levels,buildPath,bt,bo);
	}

	[PostProcessBuild(0)]
	public static void OnPostProcessBuildFirst(BuildTarget bt, string path){
		if(bt == BuildTarget.WebGL){
			moveResources("levels",false);
		}else{
			moveResources("webLevels",false);
		}
		if(bt == BuildTarget.iOS || bt == BuildTarget.StandaloneOSXUniversal){
			moveResources("achievementIcons",false);
		}
		AssetDatabase.Refresh();
		switch(lastPlatform){
			case platform.osx:
				if(PlayerSettings.useMacAppStoreValidation){
					runShell("/Volumes/Jesper Ext/unity/ZeGame/ZeGame_osx/fixAndSign.sh");
				}else{
					runShell("/Volumes/Jesper Ext/unity/ZeGame/ZeGame_osx/fixSignAndOpen.sh");
				}
				break;
			case platform.ios:
				//I know I should make the paths relative but you can't tell me what to do
				runShell("/Volumes/Jesper Ext/unity/ZeGame/ZeGame_ios/build.sh");

				//find project
				string projectPath = "/Volumes/Jesper Ext/unity/ZeGame/ZeGame_ios/xcode/Unity-iPhone.xcodeproj/project.pbxproj";
				PBXProject project = new PBXProject();
				project.ReadFromFile(projectPath);
				string targetGuid = project.TargetGuidByName("Unity-iPhone");

				//add cloudkit framework (this is still easy)
				project.AddFrameworkToProject(targetGuid,"CloudKit.framework",false);

				//copy the entitlements file, you should generate this once
				//using xcode, then put it somewhere on your disk so
				//you can copy it to this location later
				File.Copy("/Volumes/Jesper Ext/unity/ZeGame/ZeGame_ios/ZeGame.entitlements","/Volumes/Jesper Ext/unity/ZeGame/ZeGame_ios/xcode/Unity-iPhone/ZeGame.entitlements");



				//now this is where it gets messy, because the PBXProject
				//class doesn't support enabling iCloud so you'll
				//have to change the string manually
				//I suggest you build your xcode project once (without
				//playing it automatically, so you'll have to remove
				// BuildOptions.AutoRunPlayer; from the buildoptions
				//then duplicate your xcode project folder and enable
				//iCloud in one of them. You can then use a site like
				//https://www.diffnow.com/ to find the difference
				//between the two projects.
				//not all differences have to be included but these are
				//the ones I found that are necessary.

				string projectString = project.WriteToString();

				//add entitlements file
				projectString = projectString.Replace("/* Begin PBXFileReference section */",
					"/* Begin PBXFileReference section */\n\t\t244C317F1B8BE5CF00F39B20 /* ZeGame.entitlements */ = {isa = PBXFileReference; lastKnownFileType = text.xml; name = ZeGame.entitlements; path = \"Unity-iPhone/ZeGame.entitlements\"; sourceTree = \"<group>\"; };");

				//add entitlements file (again)
				projectString = projectString.Replace("/* CustomTemplate */ = {\n			isa = PBXGroup;\n			children = (",
					"/* CustomTemplate */ = {\n			isa = PBXGroup;\n			children = (\n				244C317F1B8BE5CF00F39B20 /* ZeGame.entitlements */,");

				//add some kind of entitlements command
				projectString = projectString.Replace("CLANG_WARN_DEPRECATED_OBJC_IMPLEMENTATIONS = YES;",
					"CLANG_WARN_DEPRECATED_OBJC_IMPLEMENTATIONS = YES;\n\t\t\t\tCODE_SIGN_ENTITLEMENTS = \"Unity-iPhone/ZeGame.entitlements\";");

				//add development team you'll have to replace ****
				//with your own development string, which you can find
				//in your project.pbxproj file after enabling icloud
				projectString = projectString.Replace("TargetAttributes = {",
					"TargetAttributes = {\n					1D6058900D05DD3D006BFB54 = {\n						DevelopmentTeam = ******;\n					};");

				//save the file
				File.WriteAllText(projectPath, projectString);

				break;
			case platform.windows:
				lastPlatform = platform.windows64;
				buildLast();
				break;
			case platform.windows64:
				lastPlatform = platform.windows;
				File.Delete("/Volumes/Jesper Ext/unity/ZeGame/ZeGame_windows/64/player_win_x64_s.pdb");
				File.Delete("/Volumes/Jesper Ext/unity/ZeGame/ZeGame_windows/64/player_win_x64.pdb");
				File.Delete("/Volumes/Jesper Ext/unity/ZeGame/ZeGame_windows/32/player_win_x86_s.pdb");
				File.Delete("/Volumes/Jesper Ext/unity/ZeGame/ZeGame_windows/32/player_win_x86.pdb");
				break;
			case platform.linux:
				runShell("/Volumes/Jesper Ext/unity/ZeGame/ZeGame_linux/build.sh");
				break;
		}
	}

	[MenuItem ("Build/ios")]
	static void buildIos(){
		lastPlatform = platform.ios;
		buildLast();
	}

	[MenuItem ("Build/android")]
	static void buildAndroid(){
		lastPlatform = platform.android;
		buildLast();
	}

	[MenuItem ("Build/osx")]
	static void buildOsx(){
		lastPlatform = platform.osx;
		buildLast();
	}

	[MenuItem ("Build/windows")]
	static void buildWindows(){
		lastPlatform = platform.windows;
		buildLast();
	}

	[MenuItem ("Build/linux")]
	static void buildLinux(){
		lastPlatform = platform.linux;
		buildLast();
	}

	[MenuItem ("Build/web")]
	static void buildWeb(){
		lastPlatform = platform.web;
		buildLast();
	}

	static void runShell(string path){
		Process proc = new Process{
			StartInfo = new ProcessStartInfo{
				FileName = "open",
				Arguments = "\""+path+"\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			}
		};
		proc.Start();
	}

	static void moveResources(string asset, bool toUnused = true){
		if(toUnused){
			AssetDatabase.MoveAsset("Assets/Resources/"+asset,"Assets/Unused_Resources/"+asset);
		}else{
			AssetDatabase.MoveAsset("Assets/Unused_Resources/"+asset,"Assets/Resources/"+asset);
		}
	}
}
