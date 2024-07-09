using System.Reflection;

public abstract class TestBase
{
	protected static Stream GetResource(string name)
	{
		Stream? result = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{ThisAssembly.RootNamespace}.Resources.{name}");
		return result is not null ? result : throw new ArgumentException($"No resource by the name of \"{name}\" was found.");
	}
}
