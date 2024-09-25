namespace Documentator.Plugins
{
	public interface IDocumentationPlugin
	{
		void Generate(DocumentationProject project);
		void OnGUI();
		string GetSerializedSettings();
		void LoadSettings(string settingsJson);
		bool IsDirty();
		void ResetDirtyFlag();
	}
}