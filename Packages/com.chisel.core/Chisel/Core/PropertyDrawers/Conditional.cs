using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Core
{
	public struct Condition
	{
		public Condition(string fieldToCheck, params object[] valuesToCompareWith)
		{
			FieldToCheck = fieldToCheck;
			ValuesToCompareWith = valuesToCompareWith;
		}
		public readonly string		FieldToCheck;
		public readonly object[]	ValuesToCompareWith;
	}
}
