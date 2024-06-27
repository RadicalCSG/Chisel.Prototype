namespace Chisel.Core
{
	public class ConditionalHideAttribute : ConditionalBaseAttribute
	{
		public readonly Condition Condition;

		public ConditionalHideAttribute(string fieldToCheck, params object[] valuesToCompareWith)
		{
			Condition = new Condition(fieldToCheck, valuesToCompareWith);
		}
	}
}
