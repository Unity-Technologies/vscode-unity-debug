namespace UnityDebug
{
	public class Scope
	{
		public string name;
		public int variablesReference;
		public bool expensive;
		
		public Scope (string name, int variablesReference, bool expensive = false)
		{
			this.name = name;
			this.variablesReference = variablesReference;
			this.expensive = expensive;
		}
	}
}

