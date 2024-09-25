namespace NamespaceTests
{
	/// <summary>
	/// MulticlassTest class description
	/// </summary>
	public class MulticlassTest
	{
		/// <summary>
		/// MulticlassTest.Value1 description
		/// </summary>
		public string Value1 = "value";
		
		/// <summary>
		/// Item class description
		/// <list type="bullet">
		/// <item><term>Term 1</term><description>Test description 1</description></item>
		/// <item><term>Term 2</term><description>Test description 2</description></item>
		/// <item><term>Term 3</term><description>Test description 3</description></item>
		/// </list>
		/// </summary>
		/// <remarks>
		/// <list type="table">
		///	<item><term>Remark 1</term><description>Remark <see cref="test">description</see> 1</description></item>
		///	<item><term>Remark 2</term><description>Remark description 2</description></item>
		/// </list>
		/// </remarks>
		public class Item
		{
			/// <summary>
			/// Test description
			/// </summary>
			public int Test = 999;
			
			/// <summary>
			/// Correct Big test description
			/// </summary>
			[Output]
			/// <summary>
			/// Incorrect Big test description
			/// </summary>
			public string BigTest = "Big";

			/// <summary>
			/// Small test description
			/// </summary>
			public string SmallTest { get; set; } = "Small";

			/// <summary>
			/// Big value description
			/// </summary>
			public string BigValue { get => BigTest; set => BigTest = value; }
		}
		
		/// Line 1: Value2
		/// <summary>
		/// Line 2: MulticlassTest.Value2 input description
		/// </summary>
		/// <remarks>
		/// Line 3: Value2 remarks
		/// <list type="number">
		///	<item><term>Remark 1</term><description>Remark <see cref="test2">description</see> 1</description></item>
		///	<item><term>Remark 2</term><description>Remark description 2</description></item>
		/// </list>
		/// </remarks>
		[Input] public string Value2;
	}
}