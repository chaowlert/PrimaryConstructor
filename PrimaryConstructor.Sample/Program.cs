using System;
using Microsoft.Extensions.DependencyInjection;

namespace PrimaryConstructor.Sample
{
	class Program
	{
		static void Main(string[] args)
		{
			var services = new ServiceCollection();
			services.AddSingleton<MyDependency>();
			services.AddSingleton<MyService>();
			var injector = services.BuildServiceProvider();
			var myService = injector.GetService<MyService>();

			Console.WriteLine(myService.Greeting());
		}
	}

	[PrimaryConstructor]
	public partial class MyService
	{
		private readonly MyDependency _myDependency;

		public string Greeting()
		{
			return $"Hello {_myDependency.GetName()}!";
		}
	}

	[PrimaryConstructor]
	public partial class MyDependency
	{
		public string GetName()
		{
			return "World";
		}
	}


}
