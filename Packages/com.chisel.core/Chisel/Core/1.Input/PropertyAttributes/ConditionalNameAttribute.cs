namespace Chisel.Core
{
	public class ConditionalNameAttribute : ConditionalBaseAttribute
	{
		public readonly string Name;

		public ConditionalNameAttribute(string name)
		{
			this.Name = name;
		}
	}
}
