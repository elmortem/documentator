using System.Collections.Generic;

namespace Documentator
{
	public class DocumentationProject
	{
		public List<NamespaceInfo> Namespaces { get; } = new();
	}

	public class NamespaceInfo
	{
		public string PackageName { get; set; }
		public string Name { get; set; }
		public List<DocumentationItem> Items { get; } = new();
	}

	public class DocumentationItem
	{
		public string Name { get; set; }
		public string Kind { get; set; }
		public string XmlComment { get; set; }
		public List<AttributeInfo> Attributes { get; } = new();
		public List<DocumentationItem> Members { get; } = new();
		public DocumentationItem Parent { get; set; }

		public string FullName
		{
			get
			{
				if (Parent == null || Parent.Kind == "namespace")
					return Name;
				
				return $"{Parent.FullName}.{Name}";
			}
		}

		public List<string> BaseTypes { get; set; } = new();
	}

	public class AttributeInfo
	{
		public string Name { get; set; }
		public Dictionary<string, string> Parameters { get; set; } = new();
	}
}