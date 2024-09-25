using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Documentator.Plugins;

namespace Documentator
{
	/// <summary>
	/// Markdown generator from XML documentation in comments.
	/// </summary>
	public class DocumentationGenerator : EditorWindow
	{
		private const string ConfigPath = "Documentator.json";

		private List<string> _inputFolders = new();
		private List<bool> _enableInputFolders = new();
		private string _outputFolder = "";
		private bool _writeTitle = true;
		private bool _generateTableOfContents;
		private bool _writeAttributes = true;
		private bool _inlineClasses;
		private List<string> _kindOrder = new() { "enum", "method", "property", "field" };
		private List<string> _kindTemplates = new() { "enum {0}", "method {0}", "property {0}", "field {0}" };
		private List<string> _kindTitles = new() { "Enums", "Methods", "Properties", "Field" };
		
		private Vector2 _scrollPosition;
		private bool _showKindOrderSection;
		private bool _showPluginsSection;
		private Dictionary<IDocumentationPlugin, bool> _showPlugins = new();


		private List<PluginData> _pluginConfigs = new();
		private readonly List<IDocumentationPlugin> _plugins = new();

		[Serializable]
		private class PluginData
		{
			public string name;
			public bool enabled;
			public string settings;
		}

		[Serializable]
		private class Config
		{
			public List<string> inputFolders = new();
			public List<bool> enableInputFolders = new();
			public string outputFolder = string.Empty;
			public bool writeTitle;
			public bool generateTableOfContents;
			public bool writeAttributes;
			public bool inlineClasses;
			public List<string> kindOrder = new();
			public List<string> kindTemplates = new();
			public List<string> kindTitles = new();
			public List<PluginData> pluginConfigs = new();
		}

		/// <summary>
		/// Show Documantator window.
		/// </summary>
		[MenuItem("Tools/Documentator")]
		public static void ShowWindow()
		{
			GetWindow<DocumentationGenerator>("Documentator");
		}

		private void OnEnable()
		{
			LoadConfig();
			LoadPlugins();
		}

		private void OnGUI()
		{
			GUILayout.Label("Documentation Generator", EditorStyles.boldLabel);

			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

			EditorGUILayout.LabelField("Input Folders");
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

			_writeTitle = EditorGUILayout.Toggle("Title", _writeTitle);
			_generateTableOfContents = EditorGUILayout.Toggle("Table of Contents", _generateTableOfContents);
			_writeAttributes = EditorGUILayout.Toggle("Attributes", _writeAttributes);
			_inlineClasses = EditorGUILayout.Toggle("Inline classes", _inlineClasses);
			
			if (EditorGUI.EndChangeCheck())
				SaveConfig();

			EditorGUILayout.Space();
			_showKindOrderSection = EditorGUILayout.Foldout(_showKindOrderSection, "Kind Order", true);
			if (_showKindOrderSection)
			{
				EditorGUI.indentLevel++;
				DrawKindOrderSection();
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space();
			_showPluginsSection = EditorGUILayout.Foldout(_showPluginsSection, $"Plugins ({_plugins.Count})", true);
			if (_showPluginsSection)
			{
				EditorGUI.indentLevel++;
				DrawPluginsSection();
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.EndScrollView();

			if (GUILayout.Button("Generate Documentation"))
			{
				SaveConfig();
				GenerateDocumentation();
			}
		}
		
		private void DrawKindOrderSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Kind", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Name", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Group Title", EditorStyles.boldLabel);
            GUILayout.Space(90);
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < _kindOrder.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _kindOrder[i] = EditorGUILayout.TextField(_kindOrder[i]);
                _kindTemplates[i] = EditorGUILayout.TextField(_kindTemplates[i]);
                _kindTitles[i] = EditorGUILayout.TextField(_kindTitles[i]);
                if (GUILayout.Button("↑", GUILayout.Width(30)) && i > 0)
                {
                    (_kindOrder[i - 1], _kindOrder[i]) = (_kindOrder[i], _kindOrder[i - 1]);
                    (_kindTemplates[i - 1], _kindTemplates[i]) = (_kindTemplates[i], _kindTemplates[i - 1]);
                    (_kindTitles[i - 1], _kindTitles[i]) = (_kindTitles[i], _kindTitles[i - 1]);
                }

                if (GUILayout.Button("↓", GUILayout.Width(30)) && i < _kindOrder.Count - 1)
                {
                    (_kindOrder[i + 1], _kindOrder[i]) = (_kindOrder[i], _kindOrder[i + 1]);
                    (_kindTemplates[i + 1], _kindTemplates[i]) = (_kindTemplates[i], _kindTemplates[i + 1]);
                    (_kindTitles[i + 1], _kindTitles[i]) = (_kindTitles[i], _kindTitles[i + 1]);
                }

                if (GUILayout.Button("-", GUILayout.Width(30)))
                {
                    _kindOrder.RemoveAt(i);
                    _kindTemplates.RemoveAt(i);
                    _kindTitles.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Kind"))
            {
                _kindOrder.Add("newKind");
                _kindTemplates.Add("newKind {0}");
                _kindTitles.Add("NewKinds");
            }
        }

        private void DrawPluginsSection()
        {
            for (int i = 0; i < _plugins.Count; i++)
            {
                var plugin = _plugins[i];
                var pluginData = _pluginConfigs[i];

                var pluginTitle = pluginData.name;
                if (plugin.IsDirty())
                    pluginTitle += " *";

                if (!_showPlugins.ContainsKey(plugin))
                {
					_showPlugins[plugin] = false;
                }

				_showPlugins[plugin] = EditorGUILayout.Foldout(_showPlugins[plugin], pluginTitle, true);

                if (_showPlugins[plugin])
                {
					pluginData.enabled = EditorGUILayout.Toggle("Enabled", pluginData.enabled, GUILayout.Width(20));
					if (pluginData.enabled)
					{
						EditorGUI.indentLevel++;
						plugin.OnGUI();
						EditorGUI.indentLevel--;
					}
				}
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
				_generateTableOfContents = config.generateTableOfContents;
				_writeAttributes = config.writeAttributes;
				_inlineClasses = config.inlineClasses;
				_kindOrder = config.kindOrder is { Count: > 0 } ? config.kindOrder : _kindOrder;
				_kindTemplates = config.kindTemplates;
				if (_kindTemplates == null || _kindTemplates.Count != _kindOrder.Count)
				{
					_kindTemplates = new List<string>();
					for (int i = 0; i < _kindOrder.Count; i++)
					{
						_kindTemplates.Add(_kindOrder[i] + " {0}");
					}
				}

				_kindTitles = config.kindTitles;
				if (_kindTitles == null || _kindTitles.Count != _kindOrder.Count)
				{
					_kindTitles = new List<string>();
					for (int i = 0; i < _kindOrder.Count; i++)
					{
						_kindTitles.Add(_kindOrder[i] + "s");
					}
				}

				_pluginConfigs = config.pluginConfigs ?? new List<PluginData>();
				for (int i = 0; i < _plugins.Count; i++)
				{
					var plugin = _plugins[i];
					var pluginData = _pluginConfigs.FirstOrDefault(p => p.name == plugin.GetType().Name);
					if (pluginData != null)
					{
						plugin.LoadSettings(pluginData.settings);
					}
				}
			}
		}

		private void SaveConfig()
		{
			for (int i = 0; i < _plugins.Count; i++)
			{
				var plugin = _plugins[i];
				var pluginData = _pluginConfigs[i];
				if (plugin.IsDirty())
				{
					pluginData.settings = plugin.GetSerializedSettings();
					plugin.ResetDirtyFlag();
				}
			}

			Config config = new Config
			{
				inputFolders = _inputFolders,
				enableInputFolders = _enableInputFolders,
				outputFolder = _outputFolder,
				writeTitle = _writeTitle,
				generateTableOfContents = _generateTableOfContents,
				writeAttributes = _writeAttributes,
				inlineClasses = _inlineClasses,
				kindOrder = _kindOrder,
				kindTemplates = _kindTemplates,
				kindTitles = _kindTitles,
				pluginConfigs = _pluginConfigs
			};
			string json = JsonUtility.ToJson(config);
			File.WriteAllText(ConfigPath, json);
		}

		private void LoadPlugins()
		{
			_plugins.Clear();

            // Получаем все сборки в текущем домене приложения
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    // Ищем все типы, реализующие интерфейс IDocumentationPlugin
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IDocumentationPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var pluginType in pluginTypes)
                    {
                        try
                        {
                            var plugin = (IDocumentationPlugin)Activator.CreateInstance(pluginType);
                            _plugins.Add(plugin);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error creating instance of plugin {pluginType.Name}: {ex.Message}");
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogError($"Error loading types from assembly {assembly.FullName}: {ex.Message}");
                    foreach (var loaderException in ex.LoaderExceptions)
                    {
                        Debug.LogError($"Loader Exception: {loaderException.Message}");
                    }
                }
            }

            // Инициализируем настройки плагинов
            foreach (var plugin in _plugins)
            {
                string pluginName = plugin.GetType().Name;
                PluginData pluginData = _pluginConfigs.FirstOrDefault(p => p.name == pluginName);
                if (pluginData == null)
                {
                    pluginData = new PluginData { name = pluginName, enabled = true, settings = "{}" };
                    _pluginConfigs.Add(pluginData);
                }
                plugin.LoadSettings(pluginData.settings);
            }
		}

		private void GenerateDocumentation()
		{
			if (_inputFolders.Count == 0 || string.IsNullOrEmpty(_outputFolder))
			{
				Debug.LogError("Please select at least one input folder and an output folder!");
				return;
			}

			var parser = new DocumentationParser();
			var project = new DocumentationProject();

			for (var i = 0; i < _inputFolders.Count; ++i)
			{
				if (!_enableInputFolders[i])
					continue;

				var folder = _inputFolders[i];
				project.Namespaces.AddRange(parser.ParseProject(folder).Namespaces);
			}

			var enabledPlugins = _plugins.Where((p, i) => _pluginConfigs[i].enabled).ToList();
			foreach (var plugin in enabledPlugins)
			{
				plugin.Generate(project);
			}

			var generator = new MarkdownGenerator(_writeTitle, _generateTableOfContents, _writeAttributes, _inlineClasses, _kindOrder,
				_kindTemplates, _kindTitles);
			generator.GenerateMarkdown(project, _outputFolder);

			Debug.Log("Documentation generated successfully!");
		}
	}
}