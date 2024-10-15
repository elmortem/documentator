using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Documentator
{
	public class DocumentationParser
	{
		public DocumentationProject ParseProject(string projectPath)
		{
			var project = new DocumentationProject();
			var files = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories);

			foreach (var file in files)
			{
				ParseFile(file, project);
			}

			foreach (var namespaceInfo in project.Namespaces)
			{
				namespaceInfo.PackageName = Path.GetDirectoryName(projectPath);
			}

			return project;
		}

		private void ParseFile(string filePath, DocumentationProject project)
		{
			string code = File.ReadAllText(filePath);
			SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
			CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

			var currentNamespace = "";
			var classStack = new Stack<DocumentationItem>();

			foreach (var member in root.Members)
			{
				if (member is NamespaceDeclarationSyntax namespaceDeclaration)
				{
					currentNamespace = namespaceDeclaration.Name.ToString();
					ParseNamespaceMembers(namespaceDeclaration, project, currentNamespace, classStack);
				}
				else if (member is ClassDeclarationSyntax classDeclaration)
				{
					ParseClassDeclaration(classDeclaration, project, currentNamespace, classStack);
				}
				else if (member is InterfaceDeclarationSyntax interfaceDeclaration)
				{
					ParseInterfaceDeclaration(interfaceDeclaration, project, currentNamespace, classStack);
				}
				else if (member is StructDeclarationSyntax structDeclaration)
				{
					ParseStructDeclaration(structDeclaration, project, currentNamespace, classStack);
				}
			}
		}

		private void ParseNamespaceMembers(NamespaceDeclarationSyntax namespaceDeclaration, DocumentationProject project, string currentNamespace, Stack<DocumentationItem> classStack)
		{
			foreach (var member in namespaceDeclaration.Members)
			{
				if (member is ClassDeclarationSyntax classDeclaration)
				{
					ParseClassDeclaration(classDeclaration, project, currentNamespace, classStack);
				}
				else if (member is InterfaceDeclarationSyntax interfaceDeclaration)
				{
					ParseInterfaceDeclaration(interfaceDeclaration, project, currentNamespace, classStack);
				}
				else if (member is StructDeclarationSyntax structDeclaration)
				{
					ParseStructDeclaration(structDeclaration, project, currentNamespace, classStack);
				}
			}
		}

		private void ParseClassDeclaration(ClassDeclarationSyntax classDeclaration, DocumentationProject project, string currentNamespace, Stack<DocumentationItem> classStack)
		{
			var className = classDeclaration.Identifier.Text;
			var xmlComment = classDeclaration.GetLeadingTrivia()
				.Select(t => t.GetStructure())
				.OfType<DocumentationCommentTriviaSyntax>()
				.FirstOrDefault()?.ToString() ?? string.Empty;
			
			xmlComment = CleanXmlComment(xmlComment);
			if(string.IsNullOrEmpty(xmlComment))
				return;

			var classItem = CreateItem(xmlComment, className, "class", ParseAttributes(classDeclaration.AttributeLists));

			if (classDeclaration.BaseList != null)
			{
				foreach (var baseType in classDeclaration.BaseList.Types)
				{
					classItem.BaseTypes.Add(baseType.ToString());
				}
			}

			if (classStack.Count > 0)
			{
				classStack.Peek().Members.Add(classItem);
				classItem.Parent = classStack.Peek();
			}
			else
			{
				AddToProject(project, currentNamespace, classItem);
			}

			classStack.Push(classItem);

			foreach (var member in classDeclaration.Members)
			{
				if (member is MethodDeclarationSyntax methodDeclaration)
				{
					ParseMethodDeclaration(methodDeclaration, classItem);
				}
				else if (member is PropertyDeclarationSyntax propertyDeclaration)
				{
					ParsePropertyDeclaration(propertyDeclaration, classItem);
				}
				else if (member is FieldDeclarationSyntax fieldDeclaration)
				{
					ParseFieldDeclaration(fieldDeclaration, classItem);
				}
				else if (member is ClassDeclarationSyntax nestedClassDeclaration)
				{
					ParseClassDeclaration(nestedClassDeclaration, project, currentNamespace, classStack);
				}
				else if (member is InterfaceDeclarationSyntax interfaceDeclaration)
				{
					ParseInterfaceDeclaration(interfaceDeclaration, project, currentNamespace, classStack);
				}
				else if (member is StructDeclarationSyntax structDeclaration)
				{
					ParseStructDeclaration(structDeclaration, project, currentNamespace, classStack);
				}
			}

			classStack.Pop();
		}

		private void ParseInterfaceDeclaration(InterfaceDeclarationSyntax interfaceDeclaration, DocumentationProject project, string currentNamespace, Stack<DocumentationItem> classStack)
		{
			var interfaceName = interfaceDeclaration.Identifier.Text;
			var xmlComment = interfaceDeclaration.GetLeadingTrivia()
				.Select(t => t.GetStructure())
				.OfType<DocumentationCommentTriviaSyntax>()
				.FirstOrDefault()?.ToString() ?? string.Empty;
			
			xmlComment = CleanXmlComment(xmlComment);
			if(string.IsNullOrEmpty(xmlComment))
				return;

			var interfaceItem = CreateItem(xmlComment, interfaceName, "interface", ParseAttributes(interfaceDeclaration.AttributeLists));

			if (interfaceDeclaration.BaseList != null)
			{
				foreach (var baseType in interfaceDeclaration.BaseList.Types)
				{
					interfaceItem.BaseTypes.Add(baseType.ToString());
				}
			}

			if (classStack.Count > 0)
			{
				classStack.Peek().Members.Add(interfaceItem);
				interfaceItem.Parent = classStack.Peek();
			}
			else
			{
				AddToProject(project, currentNamespace, interfaceItem);
			}

			classStack.Push(interfaceItem);

			foreach (var member in interfaceDeclaration.Members)
			{
				if (member is MethodDeclarationSyntax methodDeclaration)
				{
					ParseMethodDeclaration(methodDeclaration, interfaceItem);
				}
				else if (member is PropertyDeclarationSyntax propertyDeclaration)
				{
					ParsePropertyDeclaration(propertyDeclaration, interfaceItem);
				}
			}

			classStack.Pop();
		}

		private void ParseStructDeclaration(StructDeclarationSyntax structDeclaration, DocumentationProject project, string currentNamespace, Stack<DocumentationItem> classStack)
		{
			var structName = structDeclaration.Identifier.Text;
			var xmlComment = structDeclaration.GetLeadingTrivia()
				.Select(t => t.GetStructure())
				.OfType<DocumentationCommentTriviaSyntax>()
				.FirstOrDefault()?.ToString() ?? string.Empty;
			
			xmlComment = CleanXmlComment(xmlComment);
			if(string.IsNullOrEmpty(xmlComment))
				return;

			var structItem = CreateItem(xmlComment, structName, "struct", ParseAttributes(structDeclaration.AttributeLists));

			if (structDeclaration.BaseList != null)
			{
				foreach (var baseType in structDeclaration.BaseList.Types)
				{
					structItem.BaseTypes.Add(baseType.ToString());
				}
			}

			if (classStack.Count > 0)
			{
				classStack.Peek().Members.Add(structItem);
				structItem.Parent = classStack.Peek();
			}
			else
			{
				AddToProject(project, currentNamespace, structItem);
			}

			classStack.Push(structItem);

			foreach (var member in structDeclaration.Members)
			{
				if (member is MethodDeclarationSyntax methodDeclaration)
				{
					ParseMethodDeclaration(methodDeclaration, structItem);
				}
				else if (member is PropertyDeclarationSyntax propertyDeclaration)
				{
					ParsePropertyDeclaration(propertyDeclaration, structItem);
				}
				else if (member is FieldDeclarationSyntax fieldDeclaration)
				{
					ParseFieldDeclaration(fieldDeclaration, structItem);
				}
			}

			classStack.Pop();
		}

		private void ParseMethodDeclaration(MethodDeclarationSyntax methodDeclaration, DocumentationItem parentItem)
		{
			var methodName = methodDeclaration.Identifier.Text;
			var xmlComment = methodDeclaration.GetLeadingTrivia()
				.Select(t => t.GetStructure())
				.OfType<DocumentationCommentTriviaSyntax>()
				.FirstOrDefault()?.ToString() ?? string.Empty;
			
			xmlComment = CleanXmlComment(xmlComment);
			if(string.IsNullOrEmpty(xmlComment))
				return;

			var methodItem = CreateItem(xmlComment, methodName, "method", ParseAttributes(methodDeclaration.AttributeLists));
			methodItem.Parent = parentItem;
			parentItem.Members.Add(methodItem);
		}

		private void ParsePropertyDeclaration(PropertyDeclarationSyntax propertyDeclaration, DocumentationItem parentItem)
		{
			var propertyName = propertyDeclaration.Identifier.Text;
			var xmlComment = propertyDeclaration.GetLeadingTrivia()
				.Select(t => t.GetStructure())
				.OfType<DocumentationCommentTriviaSyntax>()
				.FirstOrDefault()?.ToString() ?? string.Empty;
			
			xmlComment = CleanXmlComment(xmlComment);
			if(string.IsNullOrEmpty(xmlComment))
				return;

			var propertyItem = CreateItem(xmlComment, propertyName, "property", ParseAttributes(propertyDeclaration.AttributeLists));
			propertyItem.Parent = parentItem;
			parentItem.Members.Add(propertyItem);
		}

		private void ParseFieldDeclaration(FieldDeclarationSyntax fieldDeclaration, DocumentationItem parentItem)
		{
			foreach (var variable in fieldDeclaration.Declaration.Variables)
			{
				var fieldName = variable.Identifier.Text;
				var xmlComment = fieldDeclaration.GetLeadingTrivia()
					.Select(t => t.GetStructure())
					.OfType<DocumentationCommentTriviaSyntax>()
					.FirstOrDefault()?.ToString() ?? string.Empty;
				
				xmlComment = CleanXmlComment(xmlComment);
				if(string.IsNullOrEmpty(xmlComment))
					return;

				var fieldItem = CreateItem(xmlComment, fieldName, "field", ParseAttributes(fieldDeclaration.AttributeLists));
				fieldItem.Parent = parentItem;
				parentItem.Members.Add(fieldItem);
			}
		}

		private List<AttributeInfo> ParseAttributes(SyntaxList<AttributeListSyntax> attributeLists)
		{
			var attributes = new List<AttributeInfo>();
			foreach (var attributeList in attributeLists)
			{
				foreach (var attribute in attributeList.Attributes)
				{
					var attr = new AttributeInfo { Name = attribute.Name.ToString() };
					if (attribute.ArgumentList != null)
					{
						foreach (var argument in attribute.ArgumentList.Arguments)
						{
							attr.Parameters[argument.NameEquals?.Name.Identifier.Text ?? ""] = argument.Expression.ToString();
						}
					}
					attributes.Add(attr);
				}
			}
			return attributes;
		}

		private DocumentationItem CreateItem(string xmlComment, string name, string kind, List<AttributeInfo> attributes)
		{
			var item = new DocumentationItem 
			{ 
				Name = name, 
				Kind = kind, 
				XmlComment = xmlComment
			};
			item.Attributes.AddRange(attributes);
			return item;
		}

		private void AddToProject(DocumentationProject project, string namespaceName, DocumentationItem item)
		{
			var namespaceInfo = project.Namespaces.FirstOrDefault(n => n.Name == namespaceName);
			if (namespaceInfo == null)
			{
				namespaceInfo = new NamespaceInfo { Name = namespaceName };
				project.Namespaces.Add(namespaceInfo);
			}

			namespaceInfo.Items.Add(item);
		}
		
		private string CleanXmlComment(string xmlComment)
		{
			if (string.IsNullOrWhiteSpace(xmlComment))
				return string.Empty;
			
			var lines = xmlComment.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			var cleanedLines = new List<string>();

			foreach (var line in lines)
			{
				var trimmedLine = line.Trim().TrimStart('/').Trim();
				cleanedLines.Add(trimmedLine);
			}

			return string.Join(Environment.NewLine, cleanedLines.Where(l => !string.IsNullOrWhiteSpace(l)));
		}
	}
}