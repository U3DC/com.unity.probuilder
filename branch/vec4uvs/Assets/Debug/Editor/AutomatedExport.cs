#pragma warning disable 0429
#pragma warning disable 0219

using UnityEngine;
using UnityEditor;
using System.Text;
using System;
using System.IO;
// using Ionic.Zip;
using System.Collections;
using System.Collections.Generic;

using System.Text.RegularExpressions;

#if !UNITY_WEBPLAYER
public class AutomatedExport : MonoBehaviour
{
	const string DEFAULT_PLIST_NAME = "plist.txt";

	// Naming and such	
	public class Config
	{
		// ...this is getting out of hand
		public Config(string _directoryParent, string _destinationPath, string _packageName, VersionMarking _versionMarking, string[] _ignore, bool _showFilePanel, string _append, string _addDefine, string _versionNumber)
		{
			directoryParent = _directoryParent;
			destinationPath = _destinationPath;
			packageName = _packageName;
			versionMarking = _versionMarking;
			ignore = _ignore;
			showFilePanel = _showFilePanel;
			append = _append;
			addDefine = _addDefine;
			versionNumber = _versionNumber;
		}

		public string directoryParent = "";
		public string destinationPath = "";
		public string packageName 	   = "";
		public VersionMarking versionMarking = VersionMarking.DateTime;
		public string versionNumber = "";
		public string[] ignore;
		public bool showFilePanel = true;
		public string append = "";
		public string addDefine = "";
	}

	const string DateTimeFormat = "MM-dd-yyyy";

	public enum VersionMarking {
		SVN,
		DateTime,
		Manual,
		None
	}

	[MenuItem("Tools/Automated Export/Export ProBuilder Resources")]
	public static void ExportReleaseResources()
	{
		string[] arg = System.Environment.GetCommandLineArgs();
		string[] addlIgnore = new string[0];
		string define = "";

		foreach(string str in arg)
		{
			if(str.StartsWith("ignore:"))
				addlIgnore = str.Replace("ignore:", "").Trim().Split(';');

			if(str.StartsWith("define:"))
				define = str.Replace("define:", "").Trim();
		}
		
		string[] ignore = new string[7 + addlIgnore.Length];
		ignore[0] = ".meta";
		// ignore[1] = "ProGrids";
		ignore[1] = "ClassesCore";
		ignore[2] = "EditorCore";
		ignore[3] = "Build Checklist";
		ignore[4] = "sublime-workspace";
		ignore[5] = "Install";
		ignore[6] = "ClassesEditing";

		for(int i = 7; i < ignore.Length; i++)
			ignore[i] = addlIgnore[i-7];

		Export(new Config(
			"Assets/ProCore",
			"../../bin/temp",
			"ProBuilder2(Resources)",
			VersionMarking.None,
			ignore,
			false,
			"",
			define,
			""));
	}

	public static void PrependDefine()
	{
		string[] arg = System.Environment.GetCommandLineArgs();
		string define = "";

		foreach(string str in arg)
		{
			if(str.StartsWith("define:"))
				define = str.Replace("define:", "").Trim();
		}

		CollectFiles("Assets/ProCore/ProBuilder", new string[] {".meta", "Debug", "AutomatedExport.cs"});

		foreach(string str in assetsToExport)
		{
			if(str.EndsWith(".cs") && !str.Contains("AutomatedExport"))
				AddDefine(str, define);
		}
	}

	/** for building dll release -- only call from probuilder-*.bat */
	// [MenuItem("Tools/Automated Export/Export Release")]
	public static void ExportRelease()
	{
		// parse args first
		// bool generateAboutPanel = true;
		bool generateAboutEntry = false;
		
		string[] arg = System.Environment.GetCommandLineArgs();
		string append = "";
		string path = "";
		string[] addlIgnore = new string[0];
		string packName = "ProBuilder2";
		bool generateZip = false;
		string define = "";
		string exportFolderPath = "";
		string folderRootName = "ProBuilder";
		string revisionNo = "";

		foreach(string str in arg)
		{
			if(str.StartsWith("exportFolderPath:"))
				exportFolderPath = str.Replace("exportFolderPath:", "").Trim();

			if(str.StartsWith("packName:"))
				packName = str.Replace("packName:","");

			if(str.StartsWith("suffix:"))
				append = str.Replace("suffix:", "");

			if(str.StartsWith("installDir:"))
				path = str.Replace("installDir:", "");

			if(str.StartsWith("ignore:"))
				addlIgnore = str.Replace("ignore:", "").Trim().Split(';');

			if(str.StartsWith("packageName:"))
				packName = str.Replace("packageName:", "").Trim();

			if(str.StartsWith("generateVersionInfo:"))
				generateAboutEntry = (str.Replace("generateVersionInfo:","").Trim() == "TRUE") ? true : false;

			if(str.StartsWith("generateZip:"))
				generateZip = (str.Replace("generateZip:","").Trim() == "TRUE") ? true : false;

			if(str.StartsWith("define:"))
				define = str.Replace("define:", "");

			if(str.StartsWith("folderRootName:"))
				folderRootName = str.Replace("folderRootName:", "");

			if(str.StartsWith("revisionNo:"))
				revisionNo = str.Replace("revisionNo:", "");
		}

		string changelog_path = "Assets/ProCore/" + folderRootName + "/About/changelog.txt";

		#if UNITY_5_1
		TextAsset changelog = (TextAsset)AssetDatabase.LoadAssetAtPath(changelog_path, typeof(TextAsset));
		#else
		TextAsset changelog = (TextAsset)AssetDatabase.LoadAssetAtPath(changelog_path, typeof(TextAsset));
		#endif

		// (?<=--\sProBuilder\s).*(?=\s\(r[0-9]{1,9})
		Match first = Regex.Match("(?<=--\\sProBuilder\\s).*(?=\\s\\(r[0-9]{1,9})", changelog.text);
		string VERSION_NUMBER = first.Success ? first.Value : "Failed parsing version number!";

		// write the about entry info file
		if(generateAboutEntry)
		{
			string hiddenVersionInfo = "Assets/ProCore/" + folderRootName + "/About/pc_AboutEntry_ProBuilder.txt";
			string versionInfoText = 
				"name: " + folderRootName + "\n" + 
				"identifier: ProBuilder2_AboutWindowIdentifier\n" +
				"version: " + VERSION_NUMBER + "\n" +
				"revision: " + (revisionNo == "" ? SvnManager.GetRevisionNumber() : revisionNo) + "\n" +
				"date: " + System.DateTime.Now.ToString(DateTimeFormat) + "\n" +
				"changelog: Assets/ProCore/" + folderRootName + "/About/changelog.txt";
			
			// name: ProBuilder
			// identifier: ProBuilder2_AboutWindowIdentifier
			// version: 2.2.5b0
			// revision: 2176
			// date: 04-18-2014
			// changelog: Assets/changelog.txt

			if(File.Exists(hiddenVersionInfo))
			{
				File.Delete(hiddenVersionInfo);
			}
			
			using (FileStream fs = File.Create(hiddenVersionInfo))
			{
				Byte[] contents = new UTF8Encoding(true).GetBytes(versionInfoText);
				fs.Write(contents, 0, contents.Length);
			}

			AssetDatabase.Refresh();
		}
		
		string[] ignore = new string[3 + addlIgnore.Length];
		ignore[0] = ".meta";
		ignore[1] = "Debug";
		ignore[2] = "Build Checklist";

		for(int i = 3; i < ignore.Length; i++)
			ignore[i] = addlIgnore[i-3];

		string buildPath = Export(new Config(
			exportFolderPath == "" ? "Assets/ProCore" : exportFolderPath,
			(path == "") ? "../../bin/Release" : path,
			packName,
			revisionNo == "" ? VersionMarking.SVN : VersionMarking.Manual,
			ignore,
			false,
			append,
			define,
			revisionNo));

		// puts a zipped copy on the desktop
		if(generateZip)
		{
			Debug.LogWarning("ZIP DISABLED");
			// string DESKTOP = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			// string TEMP_PATH = DESKTOP + "\\" + Path.GetFileNameWithoutExtension(buildPath) + " (" + VERSION_NUMBER + ")";
			
			// if(Directory.Exists(TEMP_PATH))
			// 	Directory.Delete(TEMP_PATH, true);
				
			// Directory.CreateDirectory(TEMP_PATH);

			// File.Copy(buildPath, TEMP_PATH + "\\" + Path.GetFileName(buildPath), true);
					
			// using (ZipFile zip = new ZipFile())
			// {
			// 	zip.AddDirectory(TEMP_PATH);

			// 	zip.Comment = "ProBuilder2 Zip File";

			// 	zip.Save(TEMP_PATH + ".zip");
			// }

			// // clean up
			// Directory.Delete(TEMP_PATH, true);
		}
	}

	public static string Export(Config config)
	{
		// Various checks...
		// if(!EditorUtility.DisplayDialog("SVN Check", "Have you commented out the SVN watermarks in everything?", "Yes", "I'm an idiot, no..."))
		// 	return;

		assetsToExport.Clear();
		
		CollectFiles(config.directoryParent, config.ignore);

		// Debug.Log(assetsToExport.ToFormattedString("\n"));

		if(config.addDefine != "")
		{
			foreach(string str in assetsToExport)
			{
				if(str.EndsWith(".cs") && !str.Contains("AutomatedExport"))
					AddDefine(str, config.addDefine);
			}
		}

		string versionNo = "";

		switch(config.versionMarking)
		{
			case VersionMarking.SVN:
		#if !UNITY_WEBPLAYER
				versionNo = "-v" + SvnManager.GetRevisionNumber();
		#else
				versionNo = "DO NOT USE WEBPLAYER SETTINGS";
		#endif
				break;

			case VersionMarking.DateTime:
				versionNo = System.DateTime.Now.ToString(DateTimeFormat);
				break;

			case VersionMarking.None:
				break;

			case VersionMarking.Manual:
				versionNo = "-v" + config.versionNumber;
				break;
		}
	
		string defaultPath = "";

		if(config.destinationPath == "Desktop")
		{
			defaultPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
		}
		else
		{
			string rootPath = Application.dataPath;
			string relativePath = config.destinationPath.Replace("../", "");

			// Because apparently just a plain ol' ../ doesn't work
			int recurseDirectory = config.destinationPath.Split(new string[1]{"../"}, System.StringSplitOptions.None).Length-1;
			for(int i = 0; i < recurseDirectory; i++)
			{
				if(System.IO.Directory.GetParent(rootPath) != null)
				{
					rootPath = System.IO.Directory.GetParent( rootPath ).ToString();
				}
			}
			defaultPath = rootPath + "/" + relativePath;
		}

		string savePath = (config.showFilePanel) ?
			EditorUtility.SaveFilePanel(
				"Export " + config.packageName,
				defaultPath,
				config.packageName + versionNo + config.append + ".unitypackage",
				"unitypackage") 
			:
		#if UNITY_STANDALONE_OSX
			defaultPath + "/" + config.packageName + versionNo + config.append + ".unitypackage";
		#else
			defaultPath + "\\" + config.packageName + versionNo + config.append + ".unitypackage";
		#endif

		if(savePath == null || savePath == "")
			return "";

		if(System.IO.File.Exists(savePath))
			File.Delete(savePath);
		
		AssetDatabase.ExportPackage(assetsToExport.ToArray(), savePath);//, ExportPackageOptions.Interactive);
		
		return savePath;
	}

	static List<string> assetsToExport = new List<string>();
	public static void CollectFiles(string path, string[] ignore)
	{
		foreach(string file in Directory.GetFiles(path))
		{
			bool skip = false;
			for(int i = 0; i < ignore.Length; i++)
			{
				if(file.Replace("\\", "/").Contains(ignore[i]))	
				{
					skip = true;
					break;
				}
			}

			if(!skip)
				assetsToExport.Add(file);
		}

		
		foreach(string directory in Directory.GetDirectories(path))
		{
			bool skip = false;
			for(int i = 0; i < ignore.Length; i++)
			{
				if(directory.Replace("\\", "/").Contains(ignore[i]))	
				{
					skip = true;
					break;
				}
			}

			if(!skip)
				CollectFiles(directory, ignore);
		}
	}

	public static void AddDefine(string path, string define)
	{
		StringBuilder sb = new StringBuilder();

		Encoding encoding;
		
		sb.AppendLine( "#define " + define);
		
		using (StreamReader sr = new StreamReader(path))
		{
			string line;
			encoding = sr.CurrentEncoding;
			while ((line = sr.ReadLine()) != null)
			{
				sb.AppendLine(line);
			}
		}
		
		using (StreamWriter writer = new StreamWriter(path, false, encoding))
		{
			writer.Write(sb.ToString());
		}
	}
}
#endif