using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Xml.Linq;
using Documentator.Plugins;
using UnityEngine;

namespace Documentator
{
	public class MarkdownGenerator
	{
		private static readonly List<string> _сlassKinds = new()
		{
			"class", "struct", "interface"
		};

		private readonly bool _writeTitle;
		private readonly bool _generateTableOfContents;
		private readonly bool _writeAttributes;
		private readonly bool _inlineClasses;
		private readonly List<string> _kindOrder;
		private readonly List<string> _kindTemplates;
		private readonly List<string> _kindTitles;

		public MarkdownGenerator(bool writeTitle, bool generateTableOfContents, bool writeAttributes, bool inlineClasses, List<string> kindOrder, List<string> kindTemplates, List<string> kindTitles)
		{
			_writeTitle = writeTitle;
			_generateTableOfContents = generateTableOfContents;
			_writeAttributes = writeAttributes;
			_inlineClasses = inlineClasses;
			_kindOrder = kindOrder;
			_kindTemplates = kindTemplates;
			_kindTitles = kindTitles;
		}

		public void GenerateMarkdown(DocumentationProject project, string outputFolder)
		{
			foreach (var namespaceInfo in project.Namespaces)
			{
				var namespacePath = Path.Combine(outputFolder, namespaceInfo.PackageName, FormatDirectoryName(namespaceInfo.Directory));

				foreach (var item in SortItems(namespaceInfo.Items))
				{
					GenerateItemMarkdownFile(item, namespacePath, string.Empty);
				}
			}
		}

		private void GenerateItemMarkdownFile(DocumentationItem item, string folderPath, string parentName)
		{
			if (string.IsNullOrEmpty(item.Name))
			{
				Debug.LogError($"Skipping item with empty name. Kind: {item.Kind}, XmlComment: {item.XmlComment}");
				return;
			}

			var markdown = new StringBuilder();

			if (_writeTitle)
			{
				markdown.AppendLine($"# {item.FullName}");
				markdown.AppendLine();
			}

			if (_generateTableOfContents)
			{
				markdown.AppendLine("## Table of Contents");
				GenerateTableOfContents(markdown, item);
				markdown.AppendLine();
			}

			GenerateItemMarkdown(item, markdown, 2);

			var groupedMembers = SortItems(item.Members.Where(p => !_сlassKinds.Contains(p.Kind))).GroupBy(m => m.Kind).OrderBy(g => _kindOrder.IndexOf(g.Key));

			foreach (var group in groupedMembers)
			{
				var kindIndex = _kindOrder.IndexOf(group.Key);
				if (kindIndex >= 0 && kindIndex < _kindTitles.Count)
				{
					markdown.AppendLine($"## {_kindTitles[kindIndex]}");
					markdown.AppendLine();
				}

				foreach (var member in group)
				{
					markdown.AppendLine($"### {FormatName(member.Name, member.Kind)}");
					markdown.AppendLine();

					GenerateItemMarkdown(member, markdown, 3);
				}
			}
			
			if (_inlineClasses)
			{
				foreach (var nestedItem in SortItems(item.Members.Where(m => _сlassKinds.Contains(m.Kind))))
				{
					markdown.AppendLine($"## {FormatName(nestedItem.Name, nestedItem.Kind)}");
					markdown.AppendLine();

					GenerateItemMarkdown(nestedItem, markdown, 2);
				}
			}
			
			if (!Directory.Exists(folderPath))
				Directory.CreateDirectory(folderPath);

			string fileName = FormatFileName(!string.IsNullOrEmpty(parentName) ? $"{parentName}-{item.Name}" : item.Name);
			string filePath = Path.Combine(folderPath, $"{fileName}.md");
			File.WriteAllText(filePath, markdown.ToString());

			
			if (!_inlineClasses)
			{
				foreach (var nestedItem in SortItems(item.Members.Where(m => _сlassKinds.Contains(m.Kind))))
				{
					GenerateItemMarkdownFile(nestedItem, folderPath, fileName);
				}
			}
		}

		private void GenerateItemMarkdown(DocumentationItem item, StringBuilder markdown, int level)
		{
			if (string.IsNullOrWhiteSpace(item.XmlComment))
			{
				Debug.LogWarning($"Empty XmlComment in {item.FullName}");
				return;
			}

			if (_writeAttributes && item.Attributes.Any())
			{
				foreach (var attr in item.Attributes)
				{
					markdown.AppendLine($"*{FormatAttribute(attr)}*");
				}
				markdown.AppendLine();
			}

			try
			{
				var xmlDoc = XDocument.Parse($"<root>{item.XmlComment}</root>");
            
				foreach (var element in xmlDoc.Root.Elements())
				{
					ProcessXmlElement(element, markdown, level);
				}
			}
			catch (System.Xml.XmlException ex)
			{
				Debug.LogError($"Error parsing XML comment for {item.FullName}: {ex.Message}");
				markdown.AppendLine($"Error parsing XML comment: {item.XmlComment}");
			}
		}

		private void ProcessXmlElement(XElement element, StringBuilder markdown, int level)
	    {
	        switch (element.Name.LocalName.ToLower())
	        {
	            case "summary":
	                markdown.AppendLine(FormatXmlElement(element));
	                markdown.AppendLine();
	                break;

	            case "typeparam":
	            case "param":
	                if (element == element.Parent.Elements(element.Name.LocalName).First())
	                {
	                    markdown.AppendLine($"{new string('#', level + 1)} {char.ToUpper(element.Name.LocalName[0])}{element.Name.LocalName.Substring(1)}eters");
	                }
	                var name = element.Attribute("name")?.Value;
	                markdown.AppendLine($"- `{name}`: {FormatXmlElement(element)}");
	                break;

	            case "returns":
	            case "value":
	                markdown.AppendLine($"{new string('#', level + 1)} {char.ToUpper(element.Name.LocalName[0])}{element.Name.LocalName.Substring(1)}");
	                markdown.AppendLine(FormatXmlElement(element));
	                markdown.AppendLine();
	                break;

	            case "exception":
	                if (element == element.Parent.Elements("exception").First())
	                {
	                    markdown.AppendLine($"{new string('#', level + 1)} Exceptions");
	                }
	                var cref = element.Attribute("cref")?.Value;
	                markdown.AppendLine($"- `{FormatCref(cref)}`: {FormatXmlElement(element)}");
	                break;

	            case "remarks":
	            case "example":
	                markdown.AppendLine($"{new string('#', level + 1)} {char.ToUpper(element.Name.LocalName[0])}{element.Name.LocalName.Substring(1)}");
	                markdown.AppendLine(FormatXmlElement(element));
	                markdown.AppendLine();
	                break;

	            case "seealso":
	                if (element == element.Parent.Elements("seealso").First())
	                {
	                    markdown.AppendLine($"{new string('#', level + 1)} See Also");
	                }
	                cref = element.Attribute("cref")?.Value;
	                var href = element.Attribute("href")?.Value;
	                if (!string.IsNullOrEmpty(cref))
	                {
	                    markdown.AppendLine($"- {FormatCref(cref)}");
	                }
	                else if (!string.IsNullOrEmpty(href))
	                {
	                    markdown.AppendLine($"- [{element.Value}]({href})");
	                }
	                break;

	            default:
	                ProcessCustomXmlElement(element, markdown, level);
	                break;
	        }

	        if (element == element.Parent.Elements(element.Name.LocalName).Last() && 
	            (element.Name.LocalName == "param" || element.Name.LocalName == "typeparam" || 
	             element.Name.LocalName == "exception" || element.Name.LocalName == "seealso"))
	        {
	            markdown.AppendLine();
	        }
	    }

		private string FormatName(string name, string kind)
		{
			var kindIndex = _kindOrder.IndexOf(kind);
			if (kindIndex < 0 || kindIndex >= _kindTemplates.Count)
				return name;
			return string.Format(_kindTemplates[kindIndex], name, kind);
		}

		private string FormatXmlElement(XElement element)
		{
			if (element == null)
				return string.Empty;

			var sb = new StringBuilder();

			foreach (var node in element.Nodes())
			{
				if (node is XText text)
				{
					sb.Append(text.Value);
				}
				else if (node is XElement el)
				{
					switch (el.Name.LocalName)
					{
						case "c":
							sb.Append($"`{el.Value}`");
							break;
						case "code":
							sb.AppendLine("```csharp");
							sb.AppendLine(el.Value.Trim());
							sb.AppendLine("```");
							break;
						case "para":
							sb.AppendLine();
							sb.AppendLine(FormatXmlElement(el));
							sb.AppendLine();
							break;
						case "see":
							var cref = el.Attribute("cref")?.Value;
							var href = el.Attribute("href")?.Value;
							if (!string.IsNullOrEmpty(cref))
								sb.Append($"`{FormatCref(cref)}`");
							else if (!string.IsNullOrEmpty(href))
								sb.Append($"[{el.Value}]({href})");
							break;
						case "paramref":
							var paramName = el.Attribute("name")?.Value;
							sb.Append($"`{paramName}`");
							break;
						case "typeparamref":
							var typeParamName = el.Attribute("name")?.Value;
							sb.Append($"`{typeParamName}`");
							break;
						case "list":
							sb.Append(FormatList(el));
							break;
						default:
							sb.Append(FormatXmlElement(el));
							break;
					}
				}
			}

			return sb.ToString().Trim();
		}

		private string FormatList(XElement listElement)
		{
			var sb = new StringBuilder();
			var type = listElement.Attribute("type")?.Value ?? "bullet";

			sb.AppendLine();  // Добавляем пустую строку перед списком

			switch (type)
			{
				case "bullet":
					foreach (var item in listElement.Elements("item"))
					{
						var term = item.Element("term");
						var description = item.Element("description");
						sb.AppendLine($"- {(term == null ? "" : $"**{FormatXmlElement(term)}**: ")}{FormatXmlElement(description)}");
					}
					break;
				case "number":
					int index = 1;
					foreach (var item in listElement.Elements("item"))
					{
						var term = item.Element("term");
						var description = item.Element("description");
						sb.AppendLine($"{index}. {(term == null ? "" : $"**{FormatXmlElement(term)}**: ")}{FormatXmlElement(description)}");
						index++;
					}
					break;
				case "table":
					sb.AppendLine("| Term | Description |");
					sb.AppendLine("|------|-------------|");
					foreach (var item in listElement.Elements("item"))
					{
						var term = item.Element("term");
						var description = item.Element("description");
						sb.AppendLine($"| {FormatXmlElement(term)} | {FormatXmlElement(description)} |");
					}
					break;
				default:
					foreach (var item in listElement.Elements("item"))
					{
						var term = item.Element("term");
						var description = item.Element("description");
						sb.AppendLine($"- {(term == null ? "" : $"**{FormatXmlElement(term)}**: ")}{FormatXmlElement(description)}");
					}
					break;
			}

			sb.AppendLine();  // Добавляем пустую строку после списка

			return sb.ToString();
		}

		private string FormatAttribute(AttributeInfo attr)
		{
			if (attr.Parameters.Count == 0)
			{
				return attr.Name;
			}

			var parameters = string.Join(", ", attr.Parameters.Select(p => $"{p.Key} = {p.Value}"));
			return $"{attr.Name}({parameters})";
		}

		private IEnumerable<DocumentationItem> SortItems(IEnumerable<DocumentationItem> items)
		{
			return items.OrderBy(item => _kindOrder.IndexOf(item.Kind))
						.ThenBy(item => item.Name);
		}
		
		private void GenerateTableOfContents(StringBuilder markdown, DocumentationItem item)
		{
			markdown.AppendLine($"- [{item.Kind} {item.Name}](#{FormatAnchor(item.Kind)}-{FormatAnchor(item.Name)})");
			
			var members = SortItems(item.Members);
			
			foreach (var member in members.Where(m => !_сlassKinds.Contains(m.Kind)))
			{
				markdown.AppendLine(
					$"  - [{member.Kind} {member.Name}](#{FormatAnchor(member.Kind)}-{FormatAnchor(member.Name)})");
			}

			if (_inlineClasses)
			{
				foreach (var nestedClass in members.Where(m => _сlassKinds.Contains(m.Kind)))
				{
					markdown.AppendLine($"- [{nestedClass.Kind} {nestedClass.Name}](#{FormatAnchor(nestedClass.Kind)}-{FormatAnchor(nestedClass.Name)})");
					GenerateTableOfContents(markdown, nestedClass);
				}
			}
			else
			{
				foreach (var nestedClass in members.Where(m => _сlassKinds.Contains(m.Kind)))
				{
					markdown.AppendLine($"- [{nestedClass.Kind} {nestedClass.Name}]({FormatFileName(item.Name)}-{FormatFileName(nestedClass.Name)}.md)");
				}
			}
		}

		private string FormatFileName(string name)
		{
			return string.Join("-", Regex.Matches(name, @"[A-Z][a-z]*|\d+")
				.Cast<Match>()
				.Select(m => m.Value));
		}

		private string FormatDirectoryName(string name)
		{
			return name.Replace(".", Path.DirectorySeparatorChar.ToString());
		}

		private string FormatAnchor(string name)
		{
			return FormatFileName(name).ToLower();
		}

		private string FormatCref(string cref)
		{
			if (string.IsNullOrEmpty(cref))
				return string.Empty;

			// Remove the prefix (like T:, M:, etc.)
			var parts = cref.Split(':');
			var name = parts.Length > 1 ? string.Join(":", parts.Skip(1)) : cref;

			// Replace generic type parameters
			name = Regex.Replace(name, @"`\d+", string.Empty);
			name = name.Replace("{", "<").Replace("}", ">");

			return name;
		}

		private void ProcessCustomXmlElement(XElement element, StringBuilder markdown, int level)
		{
			markdown.AppendLine($"{new string('#', level + 1)} {char.ToUpper(element.Name.LocalName[0])}{element.Name.LocalName.Substring(1)}");
			markdown.AppendLine(FormatXmlElement(element));
			markdown.AppendLine();
		}
	}
}