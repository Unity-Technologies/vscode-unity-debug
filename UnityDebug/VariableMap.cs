using System.Collections.Generic;


namespace UnityDebug
{
	public class VariableMap<T>
	{
		int id;
		readonly Dictionary<int, T> variables = new Dictionary<int, T> ();

		public VariableMap ()
		{
			Clear ();
		}

		public int Add (T value)
		{
			int currentId = id++;
			variables [currentId] = value;
			return currentId;
		}

		public void Clear ()
		{
			id = 1;
			variables.Clear ();
		}

		public bool TryGet (int variableReference, out T result)
		{
			return variables.TryGetValue (variableReference, out result);
		}
	}
}

