using System;
using UnityEngine;

namespace Chisel.Core
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
	public abstract class ConditionalBaseAttribute : PropertyAttribute
	{
	}
}
