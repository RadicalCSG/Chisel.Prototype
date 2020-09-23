using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

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
