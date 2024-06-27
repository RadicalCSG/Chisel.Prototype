namespace Chisel.Core
{
	public class ConditionalNamePartAttribute : ConditionalBaseAttribute
	{
		public readonly Condition Condition;
		public readonly string Pattern;
		public readonly string TrueName;
		public readonly string FalseName;

		public ConditionalNamePartAttribute(string pattern, string trueName, string falseName, string fieldToCheck, params object[] valuesToCompareWith)
		{
			this.Pattern = pattern;
			this.TrueName = trueName;
			this.FalseName = falseName;
			this.Condition = new Condition(fieldToCheck, valuesToCompareWith);
		}
	}
}
