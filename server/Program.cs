
using space_with_friends;

namespace server
{
	class Program
	{
		static void Main(string[] args)
		{
			log.create("logs/server.log");

			//Is this evil, yes!  What this does is forces C# to load the common assembly and 
			//then Ceres can use the assembly to get at the juicy classes inside
			msg this_is_here_to_make_csharp_load_an_assembly;

			space_with_friends.Server.start();
		}
	}
}
