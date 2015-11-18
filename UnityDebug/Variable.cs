namespace UnityDebug
{
	public class Variable
	{
		public string name;
		public string value;
		public int variablesReference;

		public Variable (string name, string value, int variablesReference)
		{
			this.name = name;
			this.value = value;
			this.variablesReference = variablesReference;
		}
	}
}

