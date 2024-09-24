using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace Documentator
{
	/// <summary>
	/// Not a complete Markdown generator from XML documentation in comments.
	/// </summary>
	public class DocumentationGenerator : EditorWindow
	{
		private const string ConfigPath = "Documentator.json";

		private List<string> _inputFolders = new();
		private List<bool> _enableInputFolders = new();
		private string _outputFolder = "";
		private bool _writeTitle;
		private Vector2 _scrollPosition;

		[System.Serializable]
		private class Config
		{
			public List<string> inputFolders = new();
			public List<bool> enableInputFolders = new();
			public string outputFolder = string.Empty;
			public bool writeTitle;
		}

		[MenuItem("Tools/Documentator")]
		// Show Documantator window.
		public static void ShowWindow()
		{
			GetWindow<DocumentationGenerator>("Documentator");
		}

		private void OnEnable()
		{
			LoadConfig();
		}

		private void OnGUI()
		{
			GUILayout.Label("Documentation Generator", EditorStyles.boldLabel);

			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

			EditorGUILayout.LabelField($"Input Folders");
			for (int i = 0; i < _inputFolders.Count; i++)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(_inputFolders[i], GUILayout.ExpandWidth(true));

				EditorGUI.BeginChangeCheck();
				_enableInputFolders[i] =
					GUILayout.Toggle(_enableInputFolders[i], string.Empty, GUILayout.MaxWidth(20f));
				if (EditorGUI.EndChangeCheck())
					SaveConfig();

				if (GUILayout.Button("Change", GUILayout.Width(60)))
				{
					int index = i;
					string path = EditorUtility.OpenFolderPanel("Select Input Folder", "", "");
					if (!string.IsNullOrEmpty(path))
					{
						_inputFolders[index] = path;
						SaveConfig();
					}
				}

				if (GUILayout.Button("Remove", GUILayout.Width(60)))
				{
					_inputFolders.RemoveAt(i);
					_enableInputFolders.RemoveAt(i);
					SaveConfig();
				}

				EditorGUILayout.EndHorizontal();
			}

			if (GUILayout.Button("Add Input Folder"))
			{
				string path = EditorUtility.OpenFolderPanel("Select Input Folder", "", "");
				if (!string.IsNullOrEmpty(path))
				{
					_inputFolders.Add(path);
					_enableInputFolders.Add(true);
					SaveConfig();
				}
			}

			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Output Folder", _outputFolder);
			if (GUILayout.Button("Change", GUILayout.Width(60)))
			{
				string path = EditorUtility.OpenFolderPanel("Select Output Folder", "", "");
				if (!string.IsNullOrEmpty(path))
				{
					_outputFolder = path;
					SaveConfig();
				}
			}

			EditorGUILayout.EndHorizontal();

			EditorGUI.BeginChangeCheck();
			_writeTitle = GUILayout.Toggle(_writeTitle, "Write Title");
			if (EditorGUI.EndChangeCheck())
				SaveConfig();

			EditorGUILayout.EndScrollView();

			if (GUILayout.Button("Generate Documentation"))
			{
				GenerateDocumentation();
			}
		}

		private void LoadConfig()
		{
			if (File.Exists(ConfigPath))
			{
				string json = File.ReadAllText(ConfigPath);
				Config config = JsonUtility.FromJson<Config>(json);
				_inputFolders = config.inputFolders ?? new();
				_enableInputFolders = config.enableInputFolders;
				if (_enableInputFolders == null || _enableInputFolders.Count != _inputFolders.Count)
				{
					_enableInputFolders = new List<bool>();
					for (int i = 0; i < _inputFolders.Count; i++)
					{
						_enableInputFolders.Add(true);
					}
				}

				_outputFolder = config.outputFolder;
				_writeTitle = config.writeTitle;
			}
		}

		private void SaveConfig()
		{
			Config config = new Config
			{
				inputFolders = _inputFolders,
				enableInputFolders = _enableInputFolders,
				outputFolder = _outputFolder,
				writeTitle = _writeTitle
			};
			string json = JsonUtility.ToJson(config);
			File.WriteAllText(ConfigPath, json);
		}

		private void GenerateDocumentation()
		{
			if (_inputFolders.Count == 0 || string.IsNullOrEmpty(_outputFolder))
			{
				Debug.LogError("Please select at least one input folder and an output folder!");
				return;
			}

			for (var i = 0; i < _inputFolders.Count; ++i)
			{
				if (!_enableInputFolders[i])
					continue;

				var folder = _inputFolders[i];
				var folderName = new DirectoryInfo(folder).Name;
				var outputSubfolder = Path.Combine(_outputFolder, folderName);

				if (!Directory.Exists(outputSubfolder))
				{
					Directory.CreateDirectory(outputSubfolder);
				}

				string[] scriptFiles = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
				foreach (var scriptFile in scriptFiles)
				{
					ProcessScript(scriptFile, outputSubfolder);
				}
			}

			Debug.Log("Documentation generated successfully!");
		}

		private void ProcessScript(string scriptPath, string outputFolder)
		{
			string[] lines = File.ReadAllLines(scriptPath);
			string className = Path.GetFileNameWithoutExtension(scriptPath);

			StringBuilder mdContent = new StringBuilder();
			StringBuilder commentBuffer = new StringBuilder();
			string currentAttribute = null;
			bool inMultiLineComment = false;
			bool isPublicClass = false;

			ClassInfo classInfo = new();

			foreach (var line in lines)
			{
				string trimmedLine = line.Trim();

				if (inMultiLineComment)
				{
					commentBuffer.AppendLine(trimmedLine);
					if (trimmedLine.EndsWith("*/"))
					{
						inMultiLineComment = false;
					}
				}
				else if (trimmedLine.StartsWith("//"))
				{
					commentBuffer.AppendLine(trimmedLine);
				}
				else if (trimmedLine.StartsWith("/*"))
				{
					inMultiLineComment = true;
					commentBuffer.AppendLine(trimmedLine);
				}
				else
				{
					if (trimmedLine.StartsWith("[Input]"))
					{
						currentAttribute = "Input";
					}
					else if (trimmedLine.StartsWith("[Output]"))
					{
						currentAttribute = "Output";
					}
					
					if (Regex.IsMatch(trimmedLine,
							@"^public\s+(?:(?:abstract|sealed|static)\s+)*(?:partial\s+)?(class|struct)"))
					{
						isPublicClass = true;
						classInfo.Comment = CleanComment(commentBuffer.ToString());

						Match classMatch = Regex.Match(trimmedLine,
							@"public\s+((?:abstract|sealed|static)\s+)*(?:partial\s+)?(class|struct)\s+(\w+)(<.*>)?");
						if (classMatch.Success)
						{
						}
					}
					else if (isPublicClass)
					{
						Match varMatch = Regex.Match(trimmedLine,
							@"^(?:\[.*?\]\s*)?public\s+((?:static|readonly|const)\s+)*(\w+(?:<.*?>)?)\s+(\w+)\s*(?:{\s*get;|;|=)");
						if (varMatch.Success)
						{
							classInfo.Variables.Add(new MemberInfo
							{
								Name = varMatch.Groups[3].Value,
								Comment = CleanComment(commentBuffer.ToString()),
								Attribute = currentAttribute
							});
						}
						else
						{
							Match methodMatch = Regex.Match(trimmedLine,
								@"^public\s+((?:static|virtual|override|abstract)\s+)*(\w+(?:<.*?>)?)\s+(\w+)\s*\((.*?)\)");
							if (methodMatch.Success)
							{
								classInfo.Methods.Add(new MemberInfo
								{
									Name = methodMatch.Groups[3].Value,
									Comment = CleanComment(commentBuffer.ToString())
								});
							}
						}
					}

					commentBuffer.Clear();
					currentAttribute = null;
				}
			}

			var hasComments = !string.IsNullOrEmpty(classInfo.Comment) ||
							classInfo.Variables.Any(v => !string.IsNullOrEmpty(v.Comment)) ||
							classInfo.Methods.Any(m => !string.IsNullOrEmpty(m.Comment));

			if (!hasComments)
			{
				Debug.Log($"No comments found for {className}. Skipping documentation generation.");
				return;
			}
			
			if (!Directory.Exists(_outputFolder))
			{
				Directory.CreateDirectory(_outputFolder);
			}

			if (_writeTitle)
			{
				mdContent.AppendLine($"# {FormatClassName(className)}");
				mdContent.AppendLine();
			}

			if (!string.IsNullOrEmpty(classInfo.Comment))
			{
				mdContent.AppendLine("## Description");
				mdContent.AppendLine(FormatComment(classInfo.Comment));
				mdContent.AppendLine();
			}

			// Sort and group variables
			var inputVariables = classInfo.Variables
				.Where(v => v.Attribute == "Input" && !string.IsNullOrEmpty(v.Comment)).ToList();
			var outputVariables = classInfo.Variables
				.Where(v => v.Attribute == "Output" && !string.IsNullOrEmpty(v.Comment)).ToList();
			var regularVariables = classInfo.Variables
				.Where(v => v.Attribute == null && !string.IsNullOrEmpty(v.Comment)).ToList();

			if (inputVariables.Any())
			{
				mdContent.AppendLine("## Input Variables");
				foreach (var variable in inputVariables)
				{
					AppendVariableInfo(mdContent, variable);
				}
			}

			if (regularVariables.Any())
			{
				mdContent.AppendLine("## Variables");
				foreach (var variable in regularVariables)
				{
					AppendVariableInfo(mdContent, variable);
				}
			}

			if (outputVariables.Any())
			{
				mdContent.AppendLine("## Output Variables");
				foreach (var variable in outputVariables)
				{
					AppendVariableInfo(mdContent, variable);
				}
			}

			var methodsWithComments = classInfo.Methods.Where(m => !string.IsNullOrEmpty(m.Comment)).ToList();
			if (methodsWithComments.Any())
			{
				mdContent.AppendLine("## Methods");
				foreach (var method in methodsWithComments)
				{
					mdContent.AppendLine($"### {method.Name}");
					mdContent.AppendLine(FormatComment(method.Comment));
					mdContent.AppendLine();
				}
			}

			string formattedFileName = FormatFileName(className);
			string outputPath = Path.Combine(outputFolder, $"{formattedFileName}.md");
			File.WriteAllText(outputPath, mdContent.ToString());

			Debug.Log($"Documentation generated for {className}");
		}

		private void AppendVariableInfo(StringBuilder mdContent, MemberInfo variable)
		{
			mdContent.AppendLine($"### {variable.Name}");
			if (!string.IsNullOrEmpty(variable.Comment))
			{
				mdContent.AppendLine(FormatComment(variable.Comment));
			}

			mdContent.AppendLine();
		}

		private string FormatComment(string comment)
		{
			StringBuilder formattedComment = new StringBuilder();

			var summaryMatch = Regex.Match(comment, @"<summary>(.*?)</summary>", RegexOptions.Singleline);
			if (summaryMatch.Success)
			{
				formattedComment.AppendLine(FormatCommentContent(summaryMatch.Groups[1].Value));
				comment = comment.Replace(summaryMatch.Value, "");
			}

			formattedComment.Append(FormatCommentContent(comment));

			return formattedComment.ToString().Trim();
		}

		private string FormatCommentContent(string content)
		{
			StringBuilder formatted = new StringBuilder();

			content = Regex.Replace(content, @"<typeparam name=""(\w+)"">(.*?)</typeparam>", m =>
				$"Where {m.Groups[1].Value}: {m.Groups[2].Value.Trim()}");

			content = Regex.Replace(content, @"<param name=""(\w+)"">(.*?)</param>", m =>
				$"Parameter '{m.Groups[1].Value}': {m.Groups[2].Value.Trim()}");

			var returnsMatch = Regex.Match(content, @"<returns>(.*?)</returns>", RegexOptions.Singleline);
			if (returnsMatch.Success)
			{
				formatted.AppendLine("#### Returns");
				string returnsContent = returnsMatch.Groups[1].Value.Trim();

				var parts = Regex.Split(returnsContent, @"(<c>.*?</c>)");

				for (int i = 0; i < parts.Length; i++)
				{
					string part = parts[i].Trim();
					if (part.StartsWith("<c>") && part.EndsWith("</c>"))
					{
						string boldContent = part.Substring(3, part.Length - 7);
						formatted.Append($"- **{boldContent}**: ");
					}
					else if (!string.IsNullOrEmpty(part))
					{
						formatted.AppendLine(part);
					}
				}

				formatted.AppendLine();
				formatted.AppendLine();
				content = content.Replace(returnsMatch.Value, "");
			}

			content = Regex.Replace(content, @"<exception cref=""(\w+)"">(.*?)</exception>", m =>
			{
				string cref = ExtractCrefName(m.Groups[1].Value);
				return $"{cref}: {m.Groups[2].Value.Trim()}";
			});

			content = Regex.Replace(content, @"<paramref name=""(\w+)""\s*/>", m => $"'{m.Groups[1].Value}'");

			content = Regex.Replace(content, @"<seealso cref=""([^""]+)""\s*/>", m =>
			{
				string cref = ExtractCrefName(m.Groups[1].Value);
				return $"See also: {cref}";
			});

			content = Regex.Replace(content, @"<see cref=""([^""]+)""\s*/>", m =>
			{
				string cref = ExtractCrefName(m.Groups[1].Value);
				return $"{cref}";
			});

			content = Regex.Replace(content, @"<(\w+)>(.*?)</\w+>", m => $"#### {m.Groups[1].Value}{m.Groups[2].Value}", RegexOptions.Singleline);
			//content = Regex.Replace(content, @"<(\w+)>", m => $"#### {m.Groups[1].Value}");
			//content = Regex.Replace(content, @"</\w+>", "");

			formatted.Append(content.Trim());
			return formatted.ToString().Trim();
		}

		private string ExtractCrefName(string cref)
		{
			int startIndex = cref.LastIndexOf('.');
			return startIndex >= 0 ? cref.Substring(startIndex + 1) : cref;
		}

		private string CleanComment(string comment)
		{
			// Remove comment symbols and leading/trailing whitespace
			string cleaned = Regex.Replace(comment, @"^[/\*\s]+|[\*/\s]+$", "", RegexOptions.Multiline).Trim();

			// Remove any remaining /// from the start of lines
			cleaned = Regex.Replace(cleaned, @"^\s*/// ?", "", RegexOptions.Multiline);

			return cleaned;
		}

		private class ClassInfo
		{
			public string Comment { get; set; }
			public List<MemberInfo> Variables { get; } = new List<MemberInfo>();
			public List<MemberInfo> Methods { get; } = new List<MemberInfo>();
		}

		private class MemberInfo
		{
			public string Name { get; set; }
			public string Comment { get; set; }
			public string Attribute { get; set; }
		}

		private string FormatFileName(string className)
		{
			string[] words = Regex.Split(className, @"(?<!^)(?=[A-Z][a-z]|(?<=[a-z])[A-Z]|\d+)");
			return string.Join("-", words);
		}

		private string FormatClassName(string className)
		{
			string[] words = Regex.Split(className, @"(?<!^)(?=[A-Z][a-z]|(?<=[a-z])[A-Z]|\d+)");

			return string.Join(" ", words);
		}
	}
}