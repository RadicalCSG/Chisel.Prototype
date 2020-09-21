using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

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
