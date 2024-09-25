/// <summary>
/// Test description
/// </summary>
public class Test
{
	// Input description
	[Input] public string TestInput;
	//TODO public string TestTodo;
	/// <summary>
	/// TestVariable description
	/// </summary>
	// Comment, not a documentation
	public string TestVariable = "test";
	// Output description
	[Output] public string TestOutput;
	/// <summary>
	/// <list type="bullet">
	///	<item><term>Item Term 1</term><description>Item description 1</description></item>
	///	<item><term>Item Term 2</term><description>Item description 2</description></item>
	/// </list>
	/// </summary>
	/// <remarks>
	/// <list type="table">
	///	<item><term>Remark 1</term><description>Remark description 1</description></item>
	///	<item><term>Remark 2</term><description>Remark description 2</description></item>
	/// </list>
	/// </remarks>
	public int TestList = 8;
}