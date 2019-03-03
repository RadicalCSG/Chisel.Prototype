using UnityEngine;

namespace UnitySceneExtensions
{
	public sealed partial class GameObjectUtility
	{
		public static bool ContainsStatic(GameObject[] objects)
		{
			if (objects == null || objects.Length == 0)
				return false;
			for (var i = 0; i < objects.Length; i++)
			{
				if (objects[i] != null && objects[i].isStatic)
					return true;
			}
			return false;
		}
	}
}